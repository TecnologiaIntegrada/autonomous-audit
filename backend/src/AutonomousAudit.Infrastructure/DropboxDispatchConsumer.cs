using System.Text.Json;
using AutonomousAudit.Application;
using AutonomousAudit.Application.Data;
using AutonomousAudit.Application.RequestHandlers;
using AutonomousAudit.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;

namespace AutonomousAudit.Infrastructure;

public sealed class DropboxDispatchConsumer : BackgroundService
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly INatsConnection _nats;
    private readonly IServiceScopeFactory _scopes;
    private readonly NatsOptions _options;
    private readonly ILogger<DropboxDispatchConsumer> _logger;

    public DropboxDispatchConsumer(
        INatsConnection nats,
        IServiceScopeFactory scopes,
        IOptions<NatsOptions> options,
        ILogger<DropboxDispatchConsumer> logger)
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
                _logger.LogError(ex, "Consumer Dropbox interrompido. Nova tentativa em 5s");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task ConsumirAsync(CancellationToken stoppingToken)
    {
        var js = new NatsJSContext(_nats);
        await js.CreateOrUpdateStreamAsync(
            new StreamConfig(_options.DropboxStream, [_options.DropboxSubject]),
            stoppingToken);

        var consumer = await js.CreateOrUpdateConsumerAsync(
            _options.DropboxStream,
            new ConsumerConfig(_options.DropboxConsumer)
            {
                FilterSubject = _options.DropboxSubject,
                AckWait = TimeSpan.FromMinutes(10)
            },
            stoppingToken);

        _logger.LogInformation(
            "Consumer Dropbox inscrito subject={Subject} stream={Stream} durable={Durable}",
            _options.DropboxSubject,
            _options.DropboxStream,
            _options.DropboxConsumer);

        await foreach (var msg in consumer.ConsumeAsync(
                           serializer: NatsRawSerializer<byte[]>.Default,
                           opts: new NatsJSConsumeOpts { MaxMsgs = 16 },
                           cancellationToken: stoppingToken))
        {
            try
            {
                await ProcessarAsync(msg.Data, stoppingToken);
                await msg.AckAsync(cancellationToken: stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Falha no processamento da mensagem dropbox tipo={Tipo} cadeia={Cadeia}. NAK para reentrega",
                    ex.GetType().Name,
                    LogExcecao.Cadeia(ex));
                try
                {
                    await msg.NakAsync(cancellationToken: stoppingToken);
                }
                catch (Exception nak)
                {
                    _logger.LogWarning(nak, "Falha ao enviar NAK da mensagem dropbox");
                }
            }
        }
    }

    private async Task ProcessarAsync(byte[]? payload, CancellationToken cancellationToken)
    {
        if (payload is null || payload.Length == 0)
        {
            _logger.LogWarning("Mensagem dropbox vazia ignorada");
            return;
        }

        var evento = JsonSerializer.Deserialize<DropboxDispatchEvento>(payload, Json);
        if (evento is null || evento.TransacaoId == Guid.Empty)
        {
            _logger.LogWarning("Mensagem dropbox sem transacaoId");
            return;
        }

        using var scope = _scopes.CreateScope();
        var dispatches = scope.ServiceProvider.GetRequiredService<IDropboxDispatchRepository>();
        var dropbox = scope.ServiceProvider.GetRequiredService<IDropboxDocumentos>();
        var staging = scope.ServiceProvider.GetRequiredService<IArquivoStaging>();

        var dispatch = await dispatches.GetByIdAsync(evento.TransacaoId, cancellationToken);
        if (dispatch is null)
        {
            throw new InvalidOperationException($"dropbox_dispatch {evento.TransacaoId} ainda nao encontrado.");
        }

        if (dispatch.Status == DropboxDispatchStatus.Sucesso)
        {
            _logger.LogInformation("Dispatch {TransacaoId} ja concluido com sucesso", dispatch.Id);
            return;
        }

        if (dispatch.Status == DropboxDispatchStatus.Falha && !dispatch.PodeTentar)
        {
            _logger.LogInformation("Dispatch {TransacaoId} ja esgotou as tentativas", dispatch.Id);
            return;
        }

        var delay = Math.Max(1, _options.DropboxRetryDelaySeconds);
        var nomeOriginal = string.IsNullOrWhiteSpace(dispatch.NomeOriginal)
            ? dispatch.NomeArquivo
            : dispatch.NomeOriginal;
        var nomeDropbox = DropboxPathHelper.NomeDropboxPorId(dispatch.ArquivoRecebidoId, nomeOriginal);
        var destinoDropbox = DropboxPathHelper.SubstituirNomeArquivo(dispatch.CaminhoDropbox, nomeDropbox);
        dispatch.DefinirNomeDropbox(nomeDropbox, destinoDropbox);

        while (dispatch.PodeTentar)
        {
            dispatch.MarcarEnviando();
            await dispatches.SaveChangesAsync(cancellationToken);
            _logger.LogInformation(
                "Enviando ao Dropbox transacao={TransacaoId} tentativa={Tentativa}/{Max} caminho={Caminho} nomeOriginal={NomeOriginal} nome={NomeArquivo}",
                dispatch.Id,
                dispatch.Tentativas + 1,
                dispatch.MaxTentativas,
                dispatch.CaminhoDropbox,
                dispatch.NomeOriginal,
                dispatch.NomeArquivo);

            try
            {
                if (!File.Exists(dispatch.CaminhoLocal))
                {
                    throw new FileNotFoundException("Binario local do ingest nao encontrado.", dispatch.CaminhoLocal);
                }

                await using var leitura = new FileStream(
                    dispatch.CaminhoLocal,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read);

                var enviado = await dropbox.EnviarAsync(dispatch.CaminhoDropbox, leitura, cancellationToken);
                if (string.IsNullOrWhiteSpace(enviado.Id) && string.IsNullOrWhiteSpace(enviado.Caminho))
                {
                    throw new InvalidOperationException("O armazenamento nao confirmou o recebimento do arquivo.");
                }

                dispatch.MarcarSucesso(enviado.Id ?? string.Empty, enviado.Caminho);
                await dispatches.SaveChangesAsync(cancellationToken);
                staging.Excluir(dispatch.CaminhoLocal);
                _logger.LogInformation(
                    "Dropbox confirmou transacao={TransacaoId} id={DropboxId} path={Caminho} tentativas={Tentativas}",
                    dispatch.Id,
                    enviado.Id,
                    enviado.Caminho,
                    dispatch.Tentativas);
                return;
            }
            catch (Exception ex)
            {
                dispatch.RegistrarTentativaFalha(ex.Message);
                await dispatches.SaveChangesAsync(cancellationToken);
                _logger.LogWarning(
                    ex,
                    "Falha Dropbox transacao={TransacaoId} tentativas={Tentativas}/{Max} status={Status}",
                    dispatch.Id,
                    dispatch.Tentativas,
                    dispatch.MaxTentativas,
                    dispatch.Status);

                if (dispatch.PodeTentar)
                {
                    var espera = TimeSpan.FromSeconds(delay * Math.Pow(2, dispatch.Tentativas - 1));
                    await Task.Delay(espera, cancellationToken);
                }
            }
        }
    }
}
