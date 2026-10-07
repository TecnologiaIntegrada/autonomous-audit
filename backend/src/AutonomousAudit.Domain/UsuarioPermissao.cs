namespace AutonomousAudit.Domain;

public class UsuarioPermissao
{
    public Guid Id { get; private set; }
    public Guid UsuarioId { get; private set; }
    public Guid ModuloId { get; private set; }
    public Guid RecursoId { get; private set; }

    public Usuario? Usuario { get; private set; }
    public Modulo? Modulo { get; private set; }
    public Recurso? Recurso { get; private set; }

    private UsuarioPermissao()
    {
    }

    public UsuarioPermissao(Guid usuarioId, Guid moduloId, Guid recursoId)
    {
        Id = Guid.NewGuid();
        UsuarioId = usuarioId;
        ModuloId = moduloId;
        RecursoId = recursoId;
    }

    public static UsuarioPermissao Hidratar(
        Guid id,
        Guid usuarioId,
        Guid moduloId,
        Guid recursoId,
        Recurso? recurso = null,
        Modulo? modulo = null)
    {
        return new UsuarioPermissao
        {
            Id = id,
            UsuarioId = usuarioId,
            ModuloId = moduloId,
            RecursoId = recursoId,
            Recurso = recurso,
            Modulo = modulo
        };
    }
}
