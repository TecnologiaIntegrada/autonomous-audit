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

public sealed class MailDispatchConsumer : BackgroundService
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly INatsConnection _nats;
    private readonly IServiceScopeFactory _scopes;
    private readonly NatsOptions _options;
    private readonly ILogger<MailDispatchConsumer> _logger;

    public MailDispatchConsumer(
        INatsConnection nats,
        IServiceScopeFactory scopes,
        IOptions<NatsOptions> options,
        ILogger<MailDispatchConsumer> logger)
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
                _logger.LogError(ex, "Consumer MAIL interrompido. Nova tentativa em 5s");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task ConsumirAsync(CancellationToken stoppingToken)
    {
        var js = new NatsJSContext(_nats);
        await js.CreateOrUpdateStreamAsync(
            new StreamConfig(_options.MailStream, [_options.MailSubject]),
            stoppingToken);

        var consumer = await js.CreateOrUpdateConsumerAsync(
            _options.MailStream,
            new ConsumerConfig(_options.MailConsumer)
            {
                FilterSubject = _options.MailSubject,
                AckWait = TimeSpan.FromMinutes(10)
            },
            stoppingToken);

        _logger.LogInformation(
            "Consumer MAIL inscrito subject={Subject} stream={Stream} durable={Durable}",
            _options.MailSubject,
            _options.MailStream,
            _options.MailConsumer);

        await foreach (var msg in consumer.ConsumeAsync(
                           serializer: NatsRawSerializer<byte[]>.Default,
                           opts: new NatsJSConsumeOpts { MaxMsgs = 8 },
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
                    "Falha no processamento MAIL tipo={Tipo} cadeia={Cadeia}. NAK para reentrega",
                    ex.GetType().Name,
                    LogExcecao.Cadeia(ex));
                try
                {
                    await msg.NakAsync(cancellationToken: stoppingToken);
                }
                catch (Exception nak)
                {
                    _logger.LogWarning(nak, "Falha ao enviar NAK MAIL");
                }
            }
        }
    }

    private async Task ProcessarAsync(byte[]? payload, CancellationToken cancellationToken)
    {
        var evento = Desserializar<MailEnvioEvento>(payload);
        if (evento is null || evento.Id == Guid.Empty)
        {
            _logger.LogWarning("Mensagem MAIL sem id");
            return;
        }

        using var scope = _scopes.CreateScope();
        var logs = scope.ServiceProvider.GetRequiredService<IMailLogRepository>();
        var contas = scope.ServiceProvider.GetRequiredService<IContaEmailRepository>();
        var sender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
        var segredos = scope.ServiceProvider.GetRequiredService<ISegredoProtector>();
        var staging = scope.ServiceProvider.GetRequiredService<IArquivoStaging>();
        var log = await logs.GetByIdAsync(evento.Id, cancellationToken)
            ?? throw new InvalidOperationException($"mail_log {evento.Id} ainda nao encontrado.");

        if (log.Status == MailLogStatus.Sucesso)
        {
            ExcluirAnexo(staging, evento.CaminhoAnexo);
            return;
        }

        if (log.Status == MailLogStatus.Falha && !log.PodeTentar)
        {
            ExcluirAnexo(staging, evento.CaminhoAnexo);
            return;
        }

        var conta = await contas.GetByIdAsync(log.ContaId, cancellationToken);
        if (conta is null)
        {
            log.MarcarFalha("Conta SMTP nao encontrada em mail_accounts.");
            await logs.SaveChangesAsync(cancellationToken);
            ExcluirAnexo(staging, evento.CaminhoAnexo);
            return;
        }

        var destinatarios = EmailDestinatarios.Parse(log.Para);
        if (destinatarios.Count == 0)
        {
            log.MarcarFalha("Nenhum destinatario valido para envio.");
            await logs.SaveChangesAsync(cancellationToken);
            ExcluirAnexo(staging, evento.CaminhoAnexo);
            return;
        }

        SmtpEnvioConfig config;
        try
        {
            config = SmtpEnvioConfigFactory.FromConta(conta, segredos.Decifrar(conta.Senha));
        }
        catch (Exception ex)
        {
            log.MarcarFalha("Falha ao decifrar a senha SMTP da conta.", ex.Message);
            await logs.SaveChangesAsync(cancellationToken);
            ExcluirAnexo(staging, evento.CaminhoAnexo);
            return;
        }

        var delay = Math.Max(1, _options.MailRetryDelaySeconds);
        while (log.PodeTentar)
        {
            log.MarcarProcessando();
            await logs.SaveChangesAsync(cancellationToken);
            try
            {
                if (!string.IsNullOrWhiteSpace(evento.CaminhoAnexo) && !File.Exists(evento.CaminhoAnexo))
                {
                    throw new FileNotFoundException("Anexo temporario nao encontrado.", evento.CaminhoAnexo);
                }

                var resultado = await sender.EnviarAsync(
                    config,
                    new EmailMensagem(
                        destinatarios,
                        log.Assunto,
                        evento.Corpo,
                        evento.Html,
                        evento.CaminhoAnexo,
                        evento.NomeAnexo),
                    cancellationToken);
                log.MarcarSucesso(resultado.RespostaServidor);
                await logs.SaveChangesAsync(cancellationToken);
                ExcluirAnexo(staging, evento.CaminhoAnexo);
                _logger.LogInformation(
                    "MAIL concluido id={Id} de={De} para={Para} tentativas={Tentativas} resposta={Resposta}",
                    log.Id,
                    log.De,
                    log.Para,
                    log.Tentativas,
                    log.RespostaServidor);
                return;
            }
            catch (Exception ex)
            {
                log.RegistrarTentativaFalha(ex.Message, ex.Message);
                await logs.SaveChangesAsync(cancellationToken);
                _logger.LogWarning(
                    ex,
                    "Falha MAIL id={Id} tentativas={Tentativas}/{Max} status={Status}",
                    log.Id,
                    log.Tentativas,
                    log.MaxTentativas,
                    log.Status);
                if (log.PodeTentar)
                {
                    await Task.Delay(TimeSpan.FromSeconds(delay * Math.Pow(2, log.Tentativas - 1)), cancellationToken);
                }
            }
        }

        ExcluirAnexo(staging, evento.CaminhoAnexo);
    }

    private static T? Desserializar<T>(byte[]? payload)
    {
        if (payload is null || payload.Length == 0)
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(payload, Json);
    }

    private static void ExcluirAnexo(IArquivoStaging staging, string? caminho)
    {
        if (!string.IsNullOrWhiteSpace(caminho))
        {
            staging.Excluir(caminho);
        }
    }
}
