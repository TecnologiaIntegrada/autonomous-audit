namespace AutonomousAudit.Domain;

public class CapturaSessao
{
    public Guid Id { get; private set; }
    public Guid CompraId { get; private set; }
    public Guid UsuarioId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public string? SessionTokenHash { get; private set; }
    public DateTimeOffset CriadoEm { get; private set; }
    public DateTimeOffset ExpiraEm { get; private set; }
    public DateTimeOffset? ConsumidoEm { get; private set; }
    public DateTimeOffset? EncerradoEm { get; private set; }

    private CapturaSessao()
    {
    }

    public static CapturaSessao Criar(Guid compraId, Guid usuarioId, string tokenHash, TimeSpan ttl) => new()
    {
        Id = Guid.NewGuid(),
        CompraId = compraId,
        UsuarioId = usuarioId,
        TokenHash = tokenHash,
        CriadoEm = DateTimeOffset.UtcNow,
        ExpiraEm = DateTimeOffset.UtcNow.Add(ttl)
    };

    public bool EstaAberta(DateTimeOffset agora) =>
        EncerradoEm is null && agora <= ExpiraEm;

    public bool PodeAbrir(DateTimeOffset agora) =>
        ConsumidoEm is null && EncerradoEm is null && agora <= ExpiraEm;

    public void Consumir(string sessionTokenHash)
    {
        ConsumidoEm = DateTimeOffset.UtcNow;
        SessionTokenHash = sessionTokenHash;
    }

    public void Encerrar() => EncerradoEm = DateTimeOffset.UtcNow;
}
