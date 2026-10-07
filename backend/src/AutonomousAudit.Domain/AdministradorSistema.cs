namespace AutonomousAudit.Domain;

public class AdministradorSistema
{
    public Guid Id { get; private set; }
    public Guid UsuarioId { get; private set; }
    public DateTimeOffset DataCriacao { get; private set; }

    public Usuario? Usuario { get; private set; }

    private AdministradorSistema()
    {
    }

    public AdministradorSistema(Guid usuarioId)
    {
        Id = Guid.NewGuid();
        UsuarioId = usuarioId;
        DataCriacao = DateTimeOffset.UtcNow;
    }
}
