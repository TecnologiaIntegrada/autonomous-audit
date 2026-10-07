using AutonomousAudit.Application;
using AutonomousAudit.Application.Data;
using AutonomousAudit.Application.RequestHandlers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PDFtoImage;
using SkiaSharp;
using UglyToad.PdfPig;

namespace AutonomousAudit.Infrastructure;

public sealed class PerplexityDocumentoPreparador : IPerplexityDocumentoPreparador
{
    private readonly PerplexityOptions _options;
    private readonly ILogger<PerplexityDocumentoPreparador> _logger;

    public PerplexityDocumentoPreparador(
        IOptions<PerplexityOptions> options,
        ILogger<PerplexityDocumentoPreparador> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public PerplexityDocumentoPreparacao Preparar(byte[]? arquivo, string? mimeType, bool forcarImagens = false)
    {
        if (arquivo is not { Length: > 0 })
        {
            return new PerplexityDocumentoPreparacao([], null);
        }

        var mime = ArquivoMime.DetectarMime(mimeType, null, arquivo) ?? mimeType;
        if (EhPdf(mime, arquivo))
        {
            if (!forcarImagens)
            {
                var texto = ExtrairTextoPdf(arquivo);
                var minChars = _options.MinCharsTextoPdf > 0 ? _options.MinCharsTextoPdf : 80;
                if (!string.IsNullOrWhiteSpace(texto) && texto.Trim().Length >= minChars)
                {
                    return new PerplexityDocumentoPreparacao([], texto);
                }
            }

            var imagens = RasterizarPdf(arquivo);
            if (imagens.Count == 0)
            {
                throw new PerplexityException(
                    PerplexityErrorKind.InvalidRequest,
                    "Nao foi possivel converter o PDF em imagens. A Agent API nao aceita PDF direto.");
            }

            return new PerplexityDocumentoPreparacao(imagens, null);
        }

        if (EhImagem(mime))
        {
            var normalizada = NormalizarImagem(arquivo, mime ?? "image/jpeg");
            return new PerplexityDocumentoPreparacao([normalizada], null);
        }

        throw new PerplexityException(
            PerplexityErrorKind.InvalidRequest,
            "Envie uma imagem (JPEG, PNG, WEBP, GIF) ou um PDF.");
    }

    private string? ExtrairTextoPdf(byte[] pdf)
    {
        try
        {
            using var stream = new MemoryStream(pdf, writable: false);
            using var documento = PdfDocument.Open(stream);
            var partes = new List<string>();
            foreach (var pagina in documento.GetPages())
            {
                var texto = pagina.Text?.Trim();
                if (!string.IsNullOrWhiteSpace(texto))
                {
                    partes.Add(texto);
                }
            }

            if (partes.Count == 0)
            {
                return null;
            }

            return string.Join("\n\n", partes);
        }
        catch (Exception ex)
        {
            _logger.LogInformation(ex, "PDF sem camada de texto utilizavel; rasterizando paginas");
            return null;
        }
    }

    private IReadOnlyList<PerplexityPaginaImagem> RasterizarPdf(byte[] pdf)
    {
        var timeoutSeconds = _options.RasterizarTimeoutSeconds > 0 ? _options.RasterizarTimeoutSeconds : 90;
        var limite = TimeSpan.FromSeconds(timeoutSeconds);
        _logger.LogInformation(
            "OCR rasterizar PDF iniciado bytes={Bytes} timeoutSeconds={TimeoutSeconds} dpi={Dpi} maxPaginas={MaxPaginas}",
            pdf.Length,
            timeoutSeconds,
            _options.DpiPdf > 0 ? _options.DpiPdf : 144,
            _options.MaxPaginasPdf > 0 ? _options.MaxPaginasPdf : 15);
        try
        {
            var imagens = RasterizarPdfNucleo(pdf)
                .WaitAsync(limite)
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();
            _logger.LogInformation(
                "OCR rasterizar PDF concluido bytes={Bytes} paginas={Paginas} timeoutSeconds={TimeoutSeconds}",
                pdf.Length,
                imagens.Count,
                timeoutSeconds);
            return imagens;
        }
        catch (TimeoutException)
        {
            _logger.LogError(
                "Timeout ao rasterizar PDF bytes={Bytes} timeoutSeconds={TimeoutSeconds}",
                pdf.Length,
                timeoutSeconds);
            throw new TimeoutException($"Timeout de {timeoutSeconds}s ao converter o PDF em imagens para o OCR.");
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Falha ao rasterizar PDF em imagens para o Perplexity bytes={Bytes} tipo={Tipo} cadeia={Cadeia}",
                pdf.Length,
                ex.GetType().Name,
                LogExcecao.Cadeia(ex));
            throw;
        }
    }

    private Task<IReadOnlyList<PerplexityPaginaImagem>> RasterizarPdfNucleo(byte[] pdf)
    {
        return Task.Run(() =>
        {
            var maxPaginas = _options.MaxPaginasPdf > 0 ? _options.MaxPaginasPdf : 15;
            var dpi = _options.DpiPdf > 0 ? _options.DpiPdf : 144;
            var imagens = new List<PerplexityPaginaImagem>();
            using var stream = new MemoryStream(pdf, writable: false);
            var pagina = 0;
#pragma warning disable CA1416
            foreach (var bitmap in Conversion.ToImages(stream, leaveOpen: true, options: new RenderOptions(Dpi: dpi)))
#pragma warning restore CA1416
            {
                using (bitmap)
                {
                    if (pagina >= maxPaginas)
                    {
                        break;
                    }

                    imagens.Add(CodificarBitmap(bitmap));
                    pagina++;
                    _logger.LogInformation(
                        "OCR rasterizar PDF pagina={Pagina} largura={Largura} altura={Altura} jpegBytes={JpegBytes}",
                        pagina,
                        imagens[^1].Largura,
                        imagens[^1].Altura,
                        imagens[^1].Bytes.Length);
                }
            }

            return (IReadOnlyList<PerplexityPaginaImagem>)imagens;
        });
    }

    private PerplexityPaginaImagem NormalizarImagem(byte[] bytes, string mime)
    {
        try
        {
            using var bitmap = SKBitmap.Decode(bytes);
            if (bitmap is null)
            {
                return new PerplexityPaginaImagem(bytes, mime, 0, 0);
            }

            return CodificarBitmap(bitmap);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao redimensionar imagem para o Perplexity");
            return new PerplexityPaginaImagem(bytes, mime, 0, 0);
        }
    }

    private PerplexityPaginaImagem CodificarBitmap(SKBitmap bitmap)
    {
        var maxLado = _options.MaxLadoImagemPx > 0 ? _options.MaxLadoImagemPx : 1600;
        var qualidade = _options.JpegQuality is >= 40 and <= 100 ? _options.JpegQuality : 80;
        SKBitmap? redimensionado = null;
        try
        {
            var origem = bitmap;
            if (bitmap.Width > maxLado || bitmap.Height > maxLado)
            {
                var escala = Math.Min((float)maxLado / bitmap.Width, (float)maxLado / bitmap.Height);
                var largura = Math.Max(1, (int)Math.Round(bitmap.Width * escala));
                var altura = Math.Max(1, (int)Math.Round(bitmap.Height * escala));
                redimensionado = bitmap.Resize(new SKImageInfo(largura, altura), new SKSamplingOptions(SKFilterMode.Linear));
                if (redimensionado is not null)
                {
                    origem = redimensionado;
                }
            }

            using var encoded = origem.Encode(SKEncodedImageFormat.Jpeg, qualidade);
            var bytes = encoded is { Size: > 0 } ? encoded.ToArray() : [];
            return new PerplexityPaginaImagem(bytes, "image/jpeg", origem.Width, origem.Height);
        }
        finally
        {
            redimensionado?.Dispose();
        }
    }

    private static bool EhPdf(string? mime, byte[] bytes) =>
        (mime ?? string.Empty).Contains("pdf", StringComparison.OrdinalIgnoreCase)
        || (bytes.Length >= 4 && bytes[0] == 0x25 && bytes[1] == 0x50 && bytes[2] == 0x44 && bytes[3] == 0x46);

    private static bool EhImagem(string? mime)
    {
        var valor = (mime ?? string.Empty).ToLowerInvariant();
        return valor.StartsWith("image/", StringComparison.Ordinal);
    }
}
