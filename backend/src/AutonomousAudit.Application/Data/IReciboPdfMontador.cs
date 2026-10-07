using AutonomousAudit.Domain;

namespace AutonomousAudit.Application.Data;

public interface IReciboPdfMontador
{
    Task<(byte[] Pdf, string Origem)> MontarAsync(
        IReadOnlyList<CompraAnexo> anexos,
        CancellationToken cancellationToken = default);
}

public interface ICompraDropboxPaths
{
    /// <summary>/AutonomousAudit/recibos/{usuarioId}/{reciboId}/{arquivoId}.pdf</summary>
    string Arquivo(Guid usuarioId, Guid reciboId, Guid arquivoId);

    /// <summary>Caminhos antigos (data/pendentes) para compatibilidade de leitura.</summary>
    IEnumerable<string> Legados(Guid reciboId, DateTimeOffset? dataCompra, DateTimeOffset dataEnvio);
}
