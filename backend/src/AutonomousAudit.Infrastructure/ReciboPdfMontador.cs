using AutonomousAudit.Application;
using AutonomousAudit.Application.Data;
using AutonomousAudit.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace AutonomousAudit.Infrastructure;

public sealed class ReciboPdfMontador : IReciboPdfMontador
{
    private static readonly TimeSpan TimeoutPorImagem = TimeSpan.FromSeconds(60);
    private readonly ILogger<ReciboPdfMontador> _logger;

    public ReciboPdfMontador(ILogger<ReciboPdfMontador> logger)
    {
        _logger = logger;
    }

    public async Task<(byte[] Pdf, string Origem)> MontarAsync(
        IReadOnlyList<CompraAnexo> anexos,
        CancellationToken cancellationToken = default)
    {
        var ordenados = anexos.OrderBy(x => x.Ordem).ToList();
        if (ordenados.Count == 0)
        {
            throw new InvalidOperationException("A compra nao tem anexos para montar o PDF.");
        }

        _logger.LogInformation("PDF recibo montagem iniciada anexos={Anexos}", ordenados.Count);

        var unicoPdf = ordenados.Count == 1 && EhPdf(ordenados[0]);
        if (unicoPdf)
        {
            var bytes = await File.ReadAllBytesAsync(ordenados[0].CaminhoLocal, cancellationToken);
            _logger.LogInformation("PDF recibo reutilizado arquivo={Arquivo} pdfBytes={PdfBytes}", ordenados[0].NomeArquivo, bytes.Length);
            return (bytes, CompraAnexoOrigem.Pdf);
        }

        var imagens = ordenados.Where(x => !EhPdf(x)).ToList();
        if (imagens.Count == 0)
        {
            var bytes = await File.ReadAllBytesAsync(ordenados[0].CaminhoLocal, cancellationToken);
            _logger.LogInformation("PDF recibo reutilizado sem imagens arquivo={Arquivo} pdfBytes={PdfBytes}", ordenados[0].NomeArquivo, bytes.Length);
            return (bytes, CompraAnexoOrigem.Pdf);
        }

        var paginas = new List<(byte[] Jpeg, int Largura, int Altura)>();
        foreach (var imagem in imagens)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var inicio = Stopwatch.StartNew();
            try
            {
                paginas.Add(await TarefaLimite.ExecutarAsync(
                    ConverterJpegAsync(imagem.CaminhoLocal, cancellationToken),
                    TimeoutPorImagem,
                    $"converter imagem {imagem.NomeArquivo}",
                    cancellationToken));
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Falha ao converter imagem do recibo arquivo={Arquivo} ordem={Ordem} caminho={Caminho} tipo={Tipo} cadeia={Cadeia} duracaoMs={DuracaoMs}",
                    imagem.NomeArquivo,
                    imagem.Ordem,
                    imagem.CaminhoLocal,
                    ex.GetType().Name,
                    LogExcecao.Cadeia(ex),
                    inicio.ElapsedMilliseconds);
                throw;
            }

            var ultima = paginas[^1];
            _logger.LogInformation(
                "PDF recibo pagina convertida arquivo={Arquivo} ordem={Ordem} jpegBytes={JpegBytes} largura={Largura} altura={Altura} duracaoMs={DuracaoMs}",
                imagem.NomeArquivo,
                imagem.Ordem,
                ultima.Jpeg.Length,
                ultima.Largura,
                ultima.Altura,
                inicio.ElapsedMilliseconds);
        }

        var pdf = JpegPdf.Montar(paginas);
        _logger.LogInformation(
            "PDF recibo montado origem={Origem} paginas={Paginas} pdfBytes={PdfBytes}",
            CompraAnexoOrigem.Imagem,
            paginas.Count,
            pdf.Length);
        return (pdf, CompraAnexoOrigem.Imagem);
    }

    private static bool EhPdf(CompraAnexo anexo) =>
        anexo.Origem == CompraAnexoOrigem.Pdf
        || anexo.Mime.Contains("pdf", StringComparison.OrdinalIgnoreCase)
        || anexo.NomeArquivo.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);

    private static async Task<(byte[] Jpeg, int Largura, int Altura)> ConverterJpegAsync(
        string caminho,
        CancellationToken cancellationToken)
    {
        await using var arquivo = File.OpenRead(caminho);
        using var original = await Image.LoadAsync(arquivo, cancellationToken);
        using var rgb = original.CloneAs<Rgb24>();
        using var jpeg = new MemoryStream();
        await rgb.SaveAsJpegAsync(jpeg, new JpegEncoder { Quality = 88 }, cancellationToken);
        return (jpeg.ToArray(), rgb.Width, rgb.Height);
    }
}

internal static class JpegPdf
{
    public static byte[] Montar(IReadOnlyList<(byte[] Jpeg, int Largura, int Altura)> paginas)
    {
        if (paginas.Count == 0)
        {
            throw new InvalidOperationException("Nenhuma imagem para montar o PDF.");
        }

        using var pdf = new MemoryStream();
        var offsets = new List<long> { 0 };
        var ascii = Encoding.ASCII;

        void Escrever(string texto) => pdf.Write(ascii.GetBytes(texto));

        int Objeto(Action<MemoryStream> corpo)
        {
            offsets.Add(pdf.Position);
            var id = offsets.Count - 1;
            Escrever($"{id} 0 obj\n");
            corpo(pdf);
            Escrever("\nendobj\n");
            return id;
        }

        Escrever("%PDF-1.4\n");

        var catalogId = 1;
        var pagesId = 2;
        offsets.Add(0);
        offsets.Add(0);

        var pageIds = new List<int>();
        foreach (var (jpeg, largura, altura) in paginas)
        {
            var w = largura.ToString(CultureInfo.InvariantCulture);
            var h = altura.ToString(CultureInfo.InvariantCulture);
            var imageId = Objeto(s =>
            {
                var header = ascii.GetBytes(
                    $"<< /Type /XObject /Subtype /Image /Width {w} /Height {h} " +
                    $"/ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Length {jpeg.Length} >>\nstream\n");
                s.Write(header);
                s.Write(jpeg);
                s.Write(ascii.GetBytes("\nendstream"));
            });
            var contentId = Objeto(s =>
            {
                var stream = ascii.GetBytes($"q {w} 0 0 {h} 0 0 cm /Im0 Do Q");
                s.Write(ascii.GetBytes($"<< /Length {stream.Length} >>\nstream\n"));
                s.Write(stream);
                s.Write(ascii.GetBytes("\nendstream"));
            });
            var pageId = Objeto(s =>
            {
                s.Write(ascii.GetBytes(
                    $"<< /Type /Page /Parent {pagesId} 0 R /MediaBox [0 0 {w} {h}] " +
                    $"/Resources << /XObject << /Im0 {imageId} 0 R >> >> /Contents {contentId} 0 R >>"));
            });
            pageIds.Add(pageId);
        }

        var kids = string.Join(" ", pageIds.Select(id => $"{id} 0 R"));
        offsets[pagesId] = pdf.Position;
        Escrever($"{pagesId} 0 obj\n<< /Type /Pages /Kids [{kids}] /Count {pageIds.Count} >>\nendobj\n");

        offsets[catalogId] = pdf.Position;
        Escrever($"{catalogId} 0 obj\n<< /Type /Catalog /Pages {pagesId} 0 R >>\nendobj\n");

        var startXref = pdf.Position;
        Escrever($"xref\n0 {offsets.Count}\n");
        Escrever("0000000000 65535 f \n");
        for (var i = 1; i < offsets.Count; i++)
        {
            Escrever($"{offsets[i]:0000000000} 00000 n \n");
        }

        Escrever($"trailer\n<< /Size {offsets.Count} /Root {catalogId} 0 R >>\nstartxref\n{startXref}\n%%EOF\n");
        return pdf.ToArray();
    }
}

public sealed class CompraDropboxPaths : ICompraDropboxPaths
{
    private readonly DropboxOptions _options;

    public CompraDropboxPaths(IOptions<DropboxOptions> options)
    {
        _options = options.Value;
    }

    public string Arquivo(Guid usuarioId, Guid reciboId, Guid arquivoId)
    {
        var raiz = Raiz();
        return $"{raiz}/recibos/{usuarioId:D}/{reciboId:D}/{arquivoId:D}.pdf";
    }

    public IEnumerable<string> Legados(Guid reciboId, DateTimeOffset? dataCompra, DateTimeOffset dataEnvio)
    {
        var raiz = Raiz();
        yield return $"{raiz}/recibos/pendentes/{reciboId:D}.pdf";
        yield return PastaPorData(raiz, reciboId, dataEnvio);
        if (dataCompra is { } data && data.UtcDateTime.Date != dataEnvio.UtcDateTime.Date)
        {
            yield return PastaPorData(raiz, reciboId, data);
        }
    }

    private static string PastaPorData(string raiz, Guid reciboId, DateTimeOffset data)
    {
        var d = data.UtcDateTime;
        return $"{raiz}/recibos/{d:yyyy}/{d:MM}/{d:yyyy-MM-dd}/{reciboId:D}.pdf";
    }

    private string Raiz()
    {
        var raiz = string.IsNullOrWhiteSpace(_options.RootPath) ? "/AutonomousAudit" : _options.RootPath.TrimEnd('/');
        return raiz.StartsWith('/') ? raiz : "/" + raiz;
    }
}
