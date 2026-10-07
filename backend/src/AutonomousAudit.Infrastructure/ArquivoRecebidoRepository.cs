using AutonomousAudit.Application.Data;
using AutonomousAudit.Domain;

namespace AutonomousAudit.Infrastructure;

public class ArquivoRecebidoRepository : IArquivoRecebidoRepository
{
    private readonly AuditDbContext _dbContext;

    public ArquivoRecebidoRepository(AuditDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(ArquivoRecebido arquivo, CancellationToken cancellationToken = default)
    {
        await _dbContext.ArquivosRecebidos.AddAsync(arquivo, cancellationToken);
    }

    public void Remove(ArquivoRecebido arquivo) =>
        _dbContext.ArquivosRecebidos.Remove(arquivo);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
