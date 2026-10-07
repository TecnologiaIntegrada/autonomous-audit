using AutonomousAudit.Domain;

namespace AutonomousAudit.Application.Data;

public interface IMailLogRepository
{
    Task AddAsync(MailLog log, CancellationToken cancellationToken = default);
    Task<MailLog?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
