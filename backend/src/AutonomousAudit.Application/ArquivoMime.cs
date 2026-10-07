namespace AutonomousAudit.Application.RequestHandlers;

public static class ArquivoMime
{
    public static string? DetectarMime(string? informado, string? nomeArquivo, byte[]? bytes)
    {
        if (!string.IsNullOrWhiteSpace(informado)
            && informado.Contains('/', StringComparison.Ordinal)
            && !informado.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase))
        {
            return informado.Trim();
        }

        var extensao = Path.GetExtension(nomeArquivo ?? string.Empty).ToLowerInvariant();
        var porExtensao = extensao switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".heic" => "image/heic",
            ".heif" => "image/heif",
            ".pdf" => "application/pdf",
            _ => null
        };
        if (porExtensao is not null)
        {
            return porExtensao;
        }

        return DetectarPorAssinatura(bytes);
    }

    public static byte[]? DecodificarBase64(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return null;
        }

        var dado = valor.Trim();
        var virgula = dado.IndexOf(',');
        if (dado.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && virgula > 0)
        {
            dado = dado[(virgula + 1)..];
        }

        try
        {
            return Convert.FromBase64String(dado);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static string? DetectarPorAssinatura(byte[]? bytes)
    {
        if (bytes is null || bytes.Length < 4)
        {
            return null;
        }

        if (bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
        {
            return "image/jpeg";
        }

        if (bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
        {
            return "image/png";
        }

        if (bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46)
        {
            return "image/gif";
        }

        if (bytes.Length >= 12
            && bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46
            && bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50)
        {
            return "image/webp";
        }

        if (bytes[0] == 0x25 && bytes[1] == 0x50 && bytes[2] == 0x44 && bytes[3] == 0x46)
        {
            return "application/pdf";
        }

        return null;
    }
}
