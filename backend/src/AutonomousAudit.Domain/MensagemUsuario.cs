using System.Text.RegularExpressions;

namespace AutonomousAudit.Domain;

public static class MensagemUsuario
{
    private static readonly Regex UrlDropbox = new(
        @"https?://[^\s]*dropbox\.com[^\s]*",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex NomeDropbox = new(
        "dropbox",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static string? OcultarProvedor(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return texto;
        }

        var semUrl = UrlDropbox.Replace(texto, "armazenamento");
        return NomeDropbox.Replace(semUrl, "armazenamento");
    }
}
