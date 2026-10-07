using AutonomousAudit.Domain;

namespace AutonomousAudit.Application.Data;

public interface IDropboxDispatchRepository
{
    Task AddAsync(DropboxDispatch dispatch, CancellationToken cancellationToken = default);
    Task<DropboxDispatch?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    void Remove(DropboxDispatch dispatch);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
