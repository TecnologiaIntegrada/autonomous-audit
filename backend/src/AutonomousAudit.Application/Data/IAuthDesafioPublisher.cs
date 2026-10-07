namespace AutonomousAudit.Application.Data;

public interface IAuthDesafioPublisher
{
    ValueTask EnfileirarSmsAsync(string numero, string mensagem, string referencia);
    ValueTask EnfileirarEmailAsync(IReadOnlyList<string> destinatarios, string assunto, string corpo);
}
