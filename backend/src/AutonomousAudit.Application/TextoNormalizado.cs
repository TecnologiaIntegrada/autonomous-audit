using System.Globalization;
using System.Text;

namespace AutonomousAudit.Application;

public static class TextoNormalizado
{
    public static string Nome(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return string.Empty;
        }

        var form = valor.Trim().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(form.Length);
        foreach (var c in form)
        {
            var categoria = CharUnicodeInfo.GetUnicodeCategory(c);
            if (categoria == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(c))
            {
                sb.Append(char.ToUpperInvariant(c));
            }
            else if (char.IsWhiteSpace(c))
            {
                if (sb.Length > 0 && sb[^1] != ' ')
                {
                    sb.Append(' ');
                }
            }
        }

        return sb.ToString().Trim();
    }

    public static string? Digitos(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return null;
        }

        var digitos = new string(valor.Where(char.IsDigit).ToArray());
        return string.IsNullOrEmpty(digitos) ? null : digitos;
    }

    public static string HashSha256Hex(byte[] bytes)
    {
        var hash = System.Security.Cryptography.SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static string HashTexto(string valor) => HashSha256Hex(Encoding.UTF8.GetBytes(valor));

    public static string TokenAleatorio(int bytes = 32)
    {
        var buffer = System.Security.Cryptography.RandomNumberGenerator.GetBytes(bytes);
        return Convert.ToHexString(buffer).ToLowerInvariant();
    }
}
