namespace AutonomousAudit.Application;

public static class FornecedorCorrespondencia
{
    public const string NomeNaoIdentificado = "Fornecedor nao identificado";
    public const string NomeNaoIdentificadoNormalizado = "FORNECEDOR NAO IDENTIFICADO";

    private static readonly HashSet<string> PalavrasIgnoradas = new(StringComparer.Ordinal)
    {
        "DE", "DA", "DO", "DOS", "DAS", "E", "EM", "NA", "NO", "NAS", "NOS",
        "LTDA", "ME", "EPP", "SA", "CIA", "COM", "S"
    };

    public static bool EhNaoIdentificado(string? nome)
    {
        var n = TextoNormalizado.Nome(nome);
        if (string.IsNullOrEmpty(n))
        {
            return true;
        }

        return n is NomeNaoIdentificadoNormalizado or "FORNECEDOR NAO ENCONTRADO"
            || (n.Contains("FORNECEDOR", StringComparison.Ordinal)
                && (n.Contains("NAO IDENTIFICADO", StringComparison.Ordinal)
                    || n.Contains("NAO ENCONTRADO", StringComparison.Ordinal)));
    }

    public static IReadOnlyList<string> TokensSignificativos(string nomeNormalizado)
    {
        if (string.IsNullOrWhiteSpace(nomeNormalizado))
        {
            return [];
        }

        return nomeNormalizado
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length >= 3 && !PalavrasIgnoradas.Contains(t))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }
}
