using AutonomousAudit.Application.Data;
using AutonomousAudit.Domain;
using Microsoft.EntityFrameworkCore;

namespace AutonomousAudit.Infrastructure;

public sealed class ContaEmailRepository : IContaEmailRepository
{
    private readonly AuditDbContext _dbContext;

    public ContaEmailRepository(AuditDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(ContaEmail conta, CancellationToken cancellationToken = default)
    {
        await _dbContext.ContasEmail.AddAsync(conta, cancellationToken);
    }

    public Task<ContaEmail?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _dbContext.ContasEmail.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<ContaEmail?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalizado = email.Trim().ToLowerInvariant();
        return _dbContext.ContasEmail.FirstOrDefaultAsync(
            x => x.Email.ToLower() == normalizado,
            cancellationToken);
    }

    public async Task<IReadOnlyList<ContaEmail>> ListAsync(CancellationToken cancellationToken = default) =>
        await _dbContext.ContasEmail.AsNoTracking().OrderBy(x => x.Email).ToListAsync(cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _dbContext.SaveChangesAsync(cancellationToken);
}
