namespace AutonomousAudit.Application;

public static class CepBrasil
{
    public static readonly IReadOnlySet<string> Ufs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "AC", "AL", "AP", "AM", "BA", "CE", "DF", "ES", "GO", "MA", "MT", "MS", "MG",
        "PA", "PB", "PR", "PE", "PI", "RJ", "RN", "RS", "RO", "RR", "SC", "SP", "SE", "TO"
    };

    public static bool TentarLer(string? entrada, out string digitos, out string formatado)
    {
        digitos = string.Empty;
        formatado = string.Empty;
        if (entrada is null)
        {
            return false;
        }

        var valor = entrada.Trim();
        if (valor.Length == 8)
        {
            if (!SomenteDigitos(valor))
            {
                return false;
            }

            digitos = valor;
            formatado = valor[..5] + "-" + valor[5..];
            return true;
        }

        if (valor.Length == 9 && valor[5] == '-')
        {
            var semHifen = string.Concat(valor.AsSpan(0, 5), valor.AsSpan(6, 3));
            if (!SomenteDigitos(semHifen))
            {
                return false;
            }

            digitos = semHifen;
            formatado = valor;
            return true;
        }

        return false;
    }

    public static string Formatado(string? cep)
    {
        return TentarLer(cep, out _, out var formatado) ? formatado : string.Empty;
    }

    private static bool SomenteDigitos(string valor)
    {
        for (var i = 0; i < valor.Length; i++)
        {
            if (!char.IsDigit(valor[i]))
            {
                return false;
            }
        }

        return true;
    }
}
