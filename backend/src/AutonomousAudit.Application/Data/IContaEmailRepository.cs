using AutonomousAudit.Domain;

namespace AutonomousAudit.Application.Data;

public interface IContaEmailRepository
{
    Task AddAsync(ContaEmail conta, CancellationToken cancellationToken = default);
    Task<ContaEmail?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ContaEmail?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ContaEmail>> ListAsync(CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
