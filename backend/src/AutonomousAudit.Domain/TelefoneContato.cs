namespace AutonomousAudit.Domain;

public static class TelefoneContato
{
    public static string? Normalizar(string? numero)
    {
        if (string.IsNullOrWhiteSpace(numero))
        {
            return null;
        }

        var bruto = numero.Trim();
        var manterMais = bruto.StartsWith('+');
        var digitos = new string(bruto.Where(char.IsDigit).ToArray());
        if (digitos.Length == 0)
        {
            return null;
        }

        return manterMais ? "+" + digitos : digitos;
    }

    public static int QuantidadeDigitos(string? numero)
    {
        if (string.IsNullOrWhiteSpace(numero))
        {
            return 0;
        }

        return numero.Count(char.IsDigit);
    }

    public static bool SaoIguais(string? a, string? b)
    {
        var da = Digitos(a);
        var db = Digitos(b);
        return da.Length > 0 && da == db;
    }

    static string Digitos(string? numero) =>
        string.IsNullOrWhiteSpace(numero)
            ? string.Empty
            : new string(numero.Where(char.IsDigit).ToArray());
}
