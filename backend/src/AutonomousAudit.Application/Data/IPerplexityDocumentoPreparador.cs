namespace AutonomousAudit.Application.Data;

public sealed record PerplexityPaginaImagem(byte[] Bytes, string MimeType, int Largura, int Altura);

public sealed record PerplexityDocumentoPreparacao(
    IReadOnlyList<PerplexityPaginaImagem> Imagens,
    string? TextoPdf);

public interface IPerplexityDocumentoPreparador
{
    PerplexityDocumentoPreparacao Preparar(byte[]? arquivo, string? mimeType, bool forcarImagens = false);
}
