using AutonomousAudit.Application.Data;
using AutonomousAudit.Domain;
using Microsoft.EntityFrameworkCore;

namespace AutonomousAudit.Infrastructure;

public sealed class DropboxDispatchRepository : IDropboxDispatchRepository
{
    private readonly AuditDbContext _db;

    public DropboxDispatchRepository(AuditDbContext db)
    {
        _db = db;
    }

    public async Task AddAsync(DropboxDispatch dispatch, CancellationToken cancellationToken = default) =>
        await _db.DropboxDispatches.AddAsync(dispatch, cancellationToken);

    public Task<DropboxDispatch?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.DropboxDispatches.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public void Remove(DropboxDispatch dispatch) =>
        _db.DropboxDispatches.Remove(dispatch);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _db.SaveChangesAsync(cancellationToken);
}
