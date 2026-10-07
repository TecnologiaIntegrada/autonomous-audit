namespace AutonomousAudit.Domain;

public class ContaEmail
{
    public Guid Id { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string Remetente { get; private set; } = string.Empty;
    public string Senha { get; private set; } = string.Empty;
    public string Servidor { get; private set; } = string.Empty;
    public int PortaSmtp { get; private set; }
    public int? PortaPop { get; private set; }
    public string Protocolo { get; private set; } = "SMTP";
    public string ModoTls { get; private set; } = "starttls";

    private ContaEmail()
    {
    }

    public ContaEmail(
        string email,
        string senha,
        string servidor,
        int portaSmtp,
        int? portaPop,
        string protocolo,
        string modoTls,
        string? remetente = null)
    {
        Id = Guid.NewGuid();
        Email = email.Trim();
        DefinirSenha(senha);
        Servidor = servidor.Trim();
        PortaSmtp = portaSmtp;
        PortaPop = portaPop;
        Protocolo = string.IsNullOrWhiteSpace(protocolo) ? "SMTP" : protocolo.Trim();
        ModoTls = string.IsNullOrWhiteSpace(modoTls) ? ModoTlsPadrao(portaSmtp) : modoTls.Trim().ToLowerInvariant();
        DefinirRemetente(remetente);
    }

    public string RemetenteEfetivo =>
        string.IsNullOrWhiteSpace(Remetente) ? Email : Remetente;

    public void DefinirSenha(string senhaCifrada)
    {
        if (string.IsNullOrWhiteSpace(senhaCifrada))
        {
            throw new ArgumentException("Senha da conta SMTP e obrigatoria.", nameof(senhaCifrada));
        }

        Senha = senhaCifrada;
    }

    public void DefinirRemetente(string? remetente)
    {
        var valor = (remetente ?? string.Empty).Trim();
        Remetente = string.IsNullOrWhiteSpace(valor) ? Email : valor;
    }

    public static string ModoTlsPadrao(int portaSmtp) =>
        portaSmtp == 465 ? "ssl" : "starttls";

    public static ContaEmail Hidratar(
        Guid id,
        string email,
        string senha,
        string servidor,
        int portaSmtp,
        int? portaPop,
        string protocolo,
        string modoTls,
        string? remetente = null)
    {
        return new ContaEmail
        {
            Id = id,
            Email = email,
            Senha = senha,
            Servidor = servidor,
            PortaSmtp = portaSmtp,
            PortaPop = portaPop,
            Protocolo = protocolo,
            ModoTls = modoTls,
            Remetente = string.IsNullOrWhiteSpace(remetente) ? email : remetente.Trim()
        };
    }
}
