using System.IO.Compression;
using System.Text;

namespace AutonomousAudit.Application;

public static class RelatorioPlanilha
{
    public static byte[] Csv(IReadOnlyList<string> cabecalho, IReadOnlyList<IReadOnlyList<string>> linhas)
    {
        var sb = new StringBuilder();
        sb.Append('\uFEFF');
        sb.AppendLine(string.Join(';', cabecalho.Select(CsvCampo)));
        foreach (var linha in linhas)
        {
            sb.AppendLine(string.Join(';', linha.Select(CsvCampo)));
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    public static byte[] Xlsx(string nomeAba, IReadOnlyList<string> cabecalho, IReadOnlyList<IReadOnlyList<string>> linhas)
    {
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, true))
        {
            Escrever(zip, "[Content_Types].xml", ContentTypes);
            Escrever(zip, "_rels/.rels", Rels);
            Escrever(zip, "xl/workbook.xml", Workbook(nomeAba));
            Escrever(zip, "xl/_rels/workbook.xml.rels", WorkbookRels);
            Escrever(zip, "xl/styles.xml", Styles);
            Escrever(zip, "xl/worksheets/sheet1.xml", Planilha(cabecalho, linhas));
        }

        return buffer.ToArray();
    }

    private static string CsvCampo(string valor)
    {
        var texto = valor ?? string.Empty;
        return $"\"{texto.Replace("\"", "\"\"")}\"";
    }

    private static void Escrever(ZipArchive zip, string nome, string xml)
    {
        var entrada = zip.CreateEntry(nome, CompressionLevel.Fastest);
        using var stream = entrada.Open();
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        writer.Write(xml);
    }

    private static string Planilha(IReadOnlyList<string> cabecalho, IReadOnlyList<IReadOnlyList<string>> linhas)
    {
        var sb = new StringBuilder();
        sb.Append("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?>""");
        sb.Append("""<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetData>""");
        sb.Append(LinhaXml(1, cabecalho, true));
        var numero = 2;
        foreach (var linha in linhas)
        {
            sb.Append(LinhaXml(numero++, linha, false));
        }

        sb.Append("</sheetData></worksheet>");
        return sb.ToString();
    }

    private static string LinhaXml(int numero, IReadOnlyList<string> valores, bool negrito)
    {
        var sb = new StringBuilder();
        sb.Append($"<row r=\"{numero}\">");
        for (var i = 0; i < valores.Count; i++)
        {
            var refCelula = $"{Coluna(i)}{numero}";
            var estilo = negrito ? " s=\"1\"" : string.Empty;
            sb.Append($"<c r=\"{refCelula}\"{estilo} t=\"inlineStr\"><is><t xml:space=\"preserve\">{Xml(valores[i])}</t></is></c>");
        }

        sb.Append("</row>");
        return sb.ToString();
    }

    private static string Coluna(int indice)
    {
        var n = indice + 1;
        var nome = string.Empty;
        while (n > 0)
        {
            n--;
            nome = (char)('A' + n % 26) + nome;
            n /= 26;
        }

        return nome;
    }

    private static string Xml(string? valor) =>
        (valor ?? string.Empty)
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;");

    private const string ContentTypes =
        """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/><Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/></Types>""";

    private const string Rels =
        """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>""";

    private const string WorkbookRels =
        """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/><Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/></Relationships>""";

    private const string Styles =
        """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><fonts count="2"><font><sz val="11"/><name val="Calibri"/></font><font><b/><sz val="11"/><name val="Calibri"/></font></fonts><fills count="1"><fill><patternFill patternType="none"/></fill></fills><borders count="1"><border/></borders><cellStyleXfs count="1"><xf/></cellStyleXfs><cellXfs count="2"><xf xfId="0"/><xf xfId="0" fontId="1"/></cellXfs></styleSheet>""";

    private static string Workbook(string nomeAba) =>
        $"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="{Xml(nomeAba)}" sheetId="1" r:id="rId1"/></sheets></workbook>""";
}
