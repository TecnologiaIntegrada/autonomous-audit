using System.Diagnostics;
using System.Text.Json;
using AutonomousAudit.Application;
using AutonomousAudit.Application.Data;
using AutonomousAudit.Application.RequestHandlers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MediatR;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;

namespace AutonomousAudit.Infrastructure;

public sealed class CompraProcessarConsumer : BackgroundService
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly INatsConnection _nats;
    private readonly IServiceScopeFactory _scopes;
    private readonly NatsOptions _options;
    private readonly ILogger<CompraProcessarConsumer> _logger;

    public CompraProcessarConsumer(
        INatsConnection nats,
        IServiceScopeFactory scopes,
        IOptions<NatsOptions> options,
        ILogger<CompraProcessarConsumer> logger)
    {
        _nats = nats;
        _scopes = scopes;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConsumirAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                using (LogExcecao.IniciarEscopo(_logger, ex))
                {
                    _logger.LogError(
                        ex,
                        "Consumer COMPRA_PROCESSAR interrompido tipo={Tipo} cadeia={Cadeia}. Nova tentativa em 5s",
                        ex.GetType().Name,
                        LogExcecao.Cadeia(ex));
                }

                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task ConsumirAsync(CancellationToken stoppingToken)
    {
        var js = new NatsJSContext(_nats);
        await js.CreateOrUpdateStreamAsync(
            new StreamConfig(_options.CompraProcessarStream, [_options.CompraProcessarSubject]),
            stoppingToken);

        var ackWait = TimeSpan.FromSeconds(ProcessarCompraJobHandler.TimeoutJobSeconds + 180);
        var consumer = await js.CreateOrUpdateConsumerAsync(
            _options.CompraProcessarStream,
            new ConsumerConfig(_options.CompraProcessarConsumer)
            {
                FilterSubject = _options.CompraProcessarSubject,
                AckWait = ackWait
            },
            stoppingToken);

        _logger.LogInformation(
            "Consumer COMPRA_PROCESSAR inscrito subject={Subject} stream={Stream} durable={Durable} ackWaitSeconds={AckWaitSeconds} jobTimeoutSeconds={JobTimeoutSeconds}",
            _options.CompraProcessarSubject,
            _options.CompraProcessarStream,
            _options.CompraProcessarConsumer,
            ackWait.TotalSeconds,
            ProcessarCompraJobHandler.TimeoutJobSeconds);

        await foreach (var msg in consumer.ConsumeAsync(
                           serializer: NatsRawSerializer<byte[]>.Default,
                           opts: new NatsJSConsumeOpts { MaxMsgs = 8 },
                           cancellationToken: stoppingToken))
        {
            Guid? compraId = TentarCompraId(msg.Data);
            var inicio = Stopwatch.StartNew();
            try
            {
                await ProcessarAsync(msg.Data, stoppingToken);
                await msg.AckAsync(cancellationToken: stoppingToken);
                _logger.LogInformation(
                    "Consumer COMPRA_PROCESSAR ACK CompraId={CompraId} DuracaoMs={DuracaoMs}",
                    compraId,
                    inicio.ElapsedMilliseconds);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                using (LogExcecao.IniciarEscopo(
                           _logger,
                           ex,
                           ("CompraId", compraId),
                           ("DuracaoMs", inicio.ElapsedMilliseconds),
                           ("CodigoCurto", compraId is Guid id ? id.ToString("N")[..8].ToUpperInvariant() : null)))
                {
                    _logger.LogError(
                        ex,
                        "Falha no job COMPRA_PROCESSAR CompraId={CompraId} DuracaoMs={DuracaoMs} tipo={Tipo} cadeia={Cadeia}. NAK para reentrega",
                        compraId,
                        inicio.ElapsedMilliseconds,
                        ex.GetType().Name,
                        LogExcecao.Cadeia(ex));
                }

                try
                {
                    await msg.NakAsync(cancellationToken: stoppingToken);
                }
                catch (Exception nak)
                {
                    _logger.LogWarning(
                        nak,
                        "Falha ao enviar NAK COMPRA_PROCESSAR CompraId={CompraId} tipo={Tipo} cadeia={Cadeia}",
                        compraId,
                        nak.GetType().Name,
                        LogExcecao.Cadeia(nak));
                }
            }
        }
    }

    private async Task ProcessarAsync(byte[]? payload, CancellationToken cancellationToken)
    {
        if (payload is null || payload.Length == 0)
        {
            _logger.LogWarning("Mensagem COMPRA_PROCESSAR vazia ignorada");
            return;
        }

        var evento = JsonSerializer.Deserialize<CompraProcessarEvento>(payload, Json);
        if (evento is null || evento.CompraId == Guid.Empty)
        {
            _logger.LogWarning(
                "Mensagem COMPRA_PROCESSAR sem compraId payloadBytes={PayloadBytes}",
                payload.Length);
            return;
        }

        var inicio = Stopwatch.StartNew();
        _logger.LogInformation(
            "Consumer COMPRA_PROCESSAR recebeu CompraId={CompraId} MensagemId={MensagemId} PayloadBytes={PayloadBytes} CodigoCurto={CodigoCurto}",
            evento.CompraId,
            evento.MensagemId,
            payload.Length,
            evento.CompraId.ToString("N")[..8].ToUpperInvariant());

        using var contexto = _logger.BeginScope(new Dictionary<string, object>
        {
            ["CompraId"] = evento.CompraId,
            ["MensagemId"] = evento.MensagemId,
            ["CodigoCurto"] = evento.CompraId.ToString("N")[..8].ToUpperInvariant()
        });

        using var scope = _scopes.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        ProcessarCompraJobResult resultado;
        try
        {
            resultado = await mediator.Send(new ProcessarCompraJobCommand(evento.CompraId), cancellationToken);
        }
        catch (Exception ex)
        {
            using (LogExcecao.IniciarEscopo(
                       _logger,
                       ex,
                       ("CompraId", evento.CompraId),
                       ("MensagemId", evento.MensagemId),
                       ("DuracaoMs", inicio.ElapsedMilliseconds)))
            {
                _logger.LogError(
                    ex,
                    "Consumer COMPRA_PROCESSAR excecao no processamento CompraId={CompraId} MensagemId={MensagemId} DuracaoMs={DuracaoMs} tipo={Tipo} cadeia={Cadeia}",
                    evento.CompraId,
                    evento.MensagemId,
                    inicio.ElapsedMilliseconds,
                    ex.GetType().Name,
                    LogExcecao.Cadeia(ex));
            }

            throw;
        }

        if (resultado is ProcessarCompraJobRetry retry)
        {
            _logger.LogWarning(
                "Consumer COMPRA_PROCESSAR retry CompraId={CompraId} MensagemId={MensagemId} DuracaoMs={DuracaoMs} Motivo={Motivo}",
                evento.CompraId,
                evento.MensagemId,
                inicio.ElapsedMilliseconds,
                retry.Message);
            throw new InvalidOperationException(retry.Message);
        }

        if (resultado is ProcessarCompraJobIgnorado ignorado)
        {
            _logger.LogWarning(
                "Consumer COMPRA_PROCESSAR ignorado CompraId={CompraId} MensagemId={MensagemId} DuracaoMs={DuracaoMs} Motivo={Motivo}",
                evento.CompraId,
                evento.MensagemId,
                inicio.ElapsedMilliseconds,
                ignorado.Message);
            return;
        }

        _logger.LogInformation(
            "Consumer COMPRA_PROCESSAR ok CompraId={CompraId} MensagemId={MensagemId} DuracaoMs={DuracaoMs}",
            evento.CompraId,
            evento.MensagemId,
            inicio.ElapsedMilliseconds);
    }

    private static Guid? TentarCompraId(byte[]? payload)
    {
        if (payload is null || payload.Length == 0)
        {
            return null;
        }

        try
        {
            var evento = JsonSerializer.Deserialize<CompraProcessarEvento>(payload, Json);
            return evento is null || evento.CompraId == Guid.Empty ? null : evento.CompraId;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
