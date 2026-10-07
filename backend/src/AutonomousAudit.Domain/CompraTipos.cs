namespace AutonomousAudit.Domain;

public static class CompraTipoDocumento
{
    public const string Recibo = "recibo";
    public const string NotaFiscal = "nota_fiscal";

    public static string Normalizar(string? valor)
    {
        var texto = (valor ?? string.Empty).Trim().ToLowerInvariant();
        if (texto is "nota_fiscal" or "nfe" or "nf-e" or "nf" or "danfe" or "nota")
        {
            return NotaFiscal;
        }

        return Recibo;
    }

    public static bool EhNotaFiscal(string? valor) => Normalizar(valor) == NotaFiscal;
}

public static class CompraTipoItem
{
    public const string Produto = "produto";
    public const string Servico = "servico";
    public const string Misto = "misto";

    public static string Normalizar(string? valor)
    {
        var texto = (valor ?? string.Empty).Trim().ToLowerInvariant();
        if (texto is "servico" or "serviço" or "servicos" or "serviços")
        {
            return Servico;
        }

        if (texto is "misto" or "mistos" or "ambos")
        {
            return Misto;
        }

        return Produto;
    }

    public static string Predominante(IEnumerable<string> tipos)
    {
        var lista = tipos.Select(Normalizar).ToList();
        if (lista.Count == 0)
        {
            return Produto;
        }

        var temProduto = lista.Any(x => x == Produto);
        var temServico = lista.Any(x => x == Servico);
        if (temProduto && temServico)
        {
            return Misto;
        }

        return temServico ? Servico : Produto;
    }

    public static bool EhServico(string? valor) => Normalizar(valor) == Servico;
}
