using AutonomousAudit.Application.Data;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace AutonomousAudit.Infrastructure;

public sealed class MailKitEmailSender : IEmailSender
{
    private readonly ILogger<MailKitEmailSender> _logger;

    public MailKitEmailSender(ILogger<MailKitEmailSender> logger)
    {
        _logger = logger;
    }

    public async Task<EmailEnvioResultado> EnviarAsync(
        SmtpEnvioConfig config,
        EmailMensagem mensagem,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(config.Host) || string.IsNullOrWhiteSpace(config.Usuario))
        {
            throw new EmailStorageException(EmailErrorKind.InvalidRequest, "Configuracao SMTP incompleta.");
        }

        var mime = MontarMensagem(config, mensagem);
        using var client = new SmtpClient();
        try
        {
            await client.ConnectAsync(config.Host, config.Porta, ToSecure(config.ModoTls), cancellationToken);
            await client.AuthenticateAsync(config.Usuario, config.Senha, cancellationToken);
            var resposta = await client.SendAsync(mime, cancellationToken);
            var detalhe = string.IsNullOrWhiteSpace(resposta) ? "250 OK" : resposta.Trim();
            _logger.LogInformation(
                "E-mail enviado via {Host}:{Porta} de {De} para {Para} resposta={Resposta}",
                config.Host,
                config.Porta,
                config.Remetente,
                string.Join("; ", mensagem.Destinatarios),
                detalhe);
            return new EmailEnvioResultado(true, detalhe);
        }
        catch (Exception ex)
        {
            var detalhe = DetalheSmtp(ex);
            _logger.LogError(
                ex,
                "Falha SMTP {Host}:{Porta} de {De} para {Para} tipo={Tipo} detalhe={Detalhe}",
                config.Host,
                config.Porta,
                config.Remetente,
                string.Join("; ", mensagem.Destinatarios),
                ex.GetType().Name,
                detalhe);
            var falha = new EmailStorageException(EmailErrorKind.Failed, detalhe, ex);
            falha.Data["Host"] = config.Host;
            falha.Data["Porta"] = config.Porta;
            falha.Data["Remetente"] = config.Remetente;
            throw falha;
        }
        finally
        {
            if (client.IsConnected)
            {
                await client.DisconnectAsync(true, cancellationToken);
            }
        }
    }

    private static MimeMessage MontarMensagem(SmtpEnvioConfig config, EmailMensagem mensagem)
    {
        var mime = new MimeMessage();
        var from = MailboxAddress.Parse(config.Remetente);
        mime.From.Add(from);
        mime.ReplyTo.Add(from);
        if (!config.Usuario.Equals(config.Remetente, StringComparison.OrdinalIgnoreCase))
        {
            mime.Sender = MailboxAddress.Parse(config.Usuario);
        }

        foreach (var destino in mensagem.Destinatarios)
        {
            mime.To.Add(MailboxAddress.Parse(destino));
        }

        mime.Subject = mensagem.Assunto;
        var builder = new BodyBuilder();
        if (mensagem.Html)
        {
            builder.HtmlBody = mensagem.Corpo;
        }
        else
        {
            builder.TextBody = mensagem.Corpo;
        }

        if (!string.IsNullOrWhiteSpace(mensagem.CaminhoAnexo) && File.Exists(mensagem.CaminhoAnexo))
        {
            var nome = string.IsNullOrWhiteSpace(mensagem.NomeAnexo)
                ? Path.GetFileName(mensagem.CaminhoAnexo)
                : mensagem.NomeAnexo;
            builder.Attachments.Add(nome, File.ReadAllBytes(mensagem.CaminhoAnexo));
        }

        mime.Body = builder.ToMessageBody();
        return mime;
    }

    private static SecureSocketOptions ToSecure(string modoTls) =>
        modoTls.Trim().ToLowerInvariant() switch
        {
            "ssl" or "smtp_ssl" => SecureSocketOptions.SslOnConnect,
            "none" or "off" => SecureSocketOptions.None,
            _ => SecureSocketOptions.StartTls
        };

    private static string DetalheSmtp(Exception ex) =>
        ex switch
        {
            SmtpCommandException smtp => $"{(int)smtp.StatusCode} {smtp.Message}".Trim(),
            SmtpProtocolException => ex.Message,
            _ => string.IsNullOrWhiteSpace(ex.Message) ? ex.GetType().Name : ex.Message
        };
}
