using AutonomousAudit.Application.Data;
using AutonomousAudit.Domain;
using Microsoft.EntityFrameworkCore;

namespace AutonomousAudit.Infrastructure;

public sealed class MailLogRepository : IMailLogRepository
{
    private readonly AuditDbContext _db;

    public MailLogRepository(AuditDbContext db)
    {
        _db = db;
    }

    public async Task AddAsync(MailLog log, CancellationToken cancellationToken = default) =>
        await _db.MailLogs.AddAsync(log, cancellationToken);

    public Task<MailLog?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.MailLogs.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _db.SaveChangesAsync(cancellationToken);
}
