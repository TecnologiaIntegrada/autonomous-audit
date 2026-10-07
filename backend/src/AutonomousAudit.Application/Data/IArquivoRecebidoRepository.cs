using AutonomousAudit.Domain;

namespace AutonomousAudit.Application.Data;

public interface IArquivoRecebidoRepository
{
    Task AddAsync(ArquivoRecebido arquivo, CancellationToken cancellationToken = default);
    void Remove(ArquivoRecebido arquivo);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
