using AutonomousAudit.Domain;

namespace AutonomousAudit.Application.Data;

public interface IUsuarioConsulta
{
    Task<Usuario?> ObterPorIdAsync(Guid id, bool incluirPermissoes, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Usuario>> ListarAsync(CancellationToken cancellationToken = default);
}

public interface ICatalogoConsulta
{
    Task<IReadOnlyList<Modulo>> ListarModulosComRecursosAsync(CancellationToken cancellationToken = default);
    Task<Modulo?> ObterModuloPorIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Recurso>> ListarRecursosAsync(Guid? moduloId, CancellationToken cancellationToken = default);
    Task<Recurso?> ObterRecursoPorIdAsync(Guid id, CancellationToken cancellationToken = default);
}

public interface IDropboxDispatchConsulta
{
    Task<DropboxDispatch?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);
}

public interface IContaEmailConsulta
{
    Task<IReadOnlyList<ContaEmail>> ListarAsync(CancellationToken cancellationToken = default);
}
