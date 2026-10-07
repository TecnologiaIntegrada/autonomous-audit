namespace AutonomousAudit.Application.Data;

public interface IEmailSender
{
    Task<EmailEnvioResultado> EnviarAsync(
        SmtpEnvioConfig config,
        EmailMensagem mensagem,
        CancellationToken cancellationToken = default);
}

public sealed record EmailEnvioResultado(bool Sucesso, string RespostaServidor);

public sealed record SmtpEnvioConfig(
    string Host,
    int Porta,
    string Usuario,
    string Senha,
    string Remetente,
    string ModoTls);

public sealed record EmailMensagem(
    IReadOnlyList<string> Destinatarios,
    string Assunto,
    string Corpo,
    bool Html,
    string? CaminhoAnexo = null,
    string? NomeAnexo = null);
