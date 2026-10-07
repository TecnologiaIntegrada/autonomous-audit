using System.Threading.Channels;
using AutonomousAudit.Application.Data;
using AutonomousAudit.Application.RequestHandlers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AutonomousAudit.Infrastructure;

public sealed class AuthDesafioWorker : BackgroundService, IAuthDesafioPublisher
{
    private readonly Channel<AuthDesafioJob> _jobs = Channel.CreateUnbounded<AuthDesafioJob>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<AuthDesafioWorker> _logger;

    public AuthDesafioWorker(IServiceScopeFactory scopes, ILogger<AuthDesafioWorker> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    public ValueTask EnfileirarSmsAsync(string numero, string mensagem, string referencia) =>
        _jobs.Writer.WriteAsync(AuthDesafioJob.Sms(numero, mensagem, referencia));

    public ValueTask EnfileirarEmailAsync(IReadOnlyList<string> destinatarios, string assunto, string corpo) =>
        _jobs.Writer.WriteAsync(AuthDesafioJob.Email(destinatarios, assunto, corpo));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var job in _jobs.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessarAsync(job, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Falha ao enviar desafio de autenticacao tipo={Tipo} referencia={Referencia} numero={NumeroSufixo}",
                    job.Tipo,
                    job.Referencia,
                    SmsNumero.Sufixo(job.Numero));
            }
        }
    }

    private async Task ProcessarAsync(AuthDesafioJob job, CancellationToken stoppingToken)
    {
        using var scope = _scopes.CreateScope();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        cts.CancelAfter(TimeSpan.FromSeconds(45));

        if (job.Tipo == "sms")
        {
            var sms = scope.ServiceProvider.GetRequiredService<ISmsDevClient>();
            var envio = await sms.EnviarAsync(job.Numero, job.Mensagem, job.Referencia, cts.Token);
            if (!envio.Situacao.Equals("OK", StringComparison.OrdinalIgnoreCase))
            {
                var recusa = new SmsDevException(
                    SmsDevErrorKind.Failed,
                    $"SMSDev recusou o envio. situacao={envio.Situacao} codigo={envio.Codigo} descricao={envio.Descricao}",
                    responseBody: $"{envio.Situacao} {envio.Codigo} {envio.Descricao}".Trim());
                recusa.Data["CodigoSmsDev"] = envio.Codigo;
                recusa.Data["Situacao"] = envio.Situacao;
                recusa.Data["Id"] = envio.Id;
                _logger.LogError(
                    recusa,
                    "SMS de autenticacao recusado referencia={Referencia} situacao={Situacao} codigoSmsDev={Codigo} descricao={Descricao}",
                    job.Referencia,
                    envio.Situacao,
                    envio.Codigo,
                    envio.Descricao);
                return;
            }

            _logger.LogInformation("SMS de autenticacao enviado referencia={Referencia}", job.Referencia);
            return;
        }

        var titan = scope.ServiceProvider.GetRequiredService<ITitanSmtpSettings>();
        if (!titan.EstaConfigurado)
        {
            var ex = new EmailStorageException(
                EmailErrorKind.InvalidRequest,
                "SMTP Titan nao configurado. Informe Smtp:Titan ou WKF_SMTP_*.");
            ex.Data["ConfigKeys"] = "Smtp:Titan, WKF_SMTP_HOST, WKF_SMTP_USER, WKF_SMTP_PASSWORD";
            _logger.LogError(ex, "SMTP Titan nao configurado para desafio de e-mail referencia={Referencia}", job.Referencia);
            return;
        }

        var emails = scope.ServiceProvider.GetRequiredService<IEmailSender>();
        await emails.EnviarAsync(
            titan.ToConfig(),
            new EmailMensagem(job.Destinatarios, job.Assunto, job.Corpo, Html: false),
            cts.Token);
        _logger.LogInformation("E-mail de autenticacao enviado referencia={Referencia}", job.Referencia);
    }

    private sealed record AuthDesafioJob(
        string Tipo,
        string Numero,
        string Mensagem,
        string Referencia,
        IReadOnlyList<string> Destinatarios,
        string Assunto,
        string Corpo)
    {
        public static AuthDesafioJob Sms(string numero, string mensagem, string referencia) =>
            new("sms", numero, mensagem, referencia, [], string.Empty, string.Empty);

        public static AuthDesafioJob Email(IReadOnlyList<string> destinatarios, string assunto, string corpo) =>
            new("email", string.Empty, string.Empty, destinatarios.FirstOrDefault() ?? string.Empty, destinatarios, assunto, corpo);
    }
}
