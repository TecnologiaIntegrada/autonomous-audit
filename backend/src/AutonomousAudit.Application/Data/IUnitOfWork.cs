namespace AutonomousAudit.Application.Data;

public interface IUnitOfWork
{
    bool HasChanges { get; }
    bool HasActiveTransaction { get; }
    Task BeginAsync(CancellationToken cancellationToken = default);
    Task CommitAsync(CancellationToken cancellationToken = default);
    Task RollbackAsync(CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
