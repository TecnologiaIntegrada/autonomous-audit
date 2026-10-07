namespace AutonomousAudit.Domain;

public class CompraItem
{
    public Guid Id { get; private set; }
    public Guid CompraId { get; private set; }
    public int Ordem { get; private set; }
    public string DescricaoOriginal { get; private set; } = string.Empty;
    public string? CodigoImpresso { get; private set; }
    public decimal Quantidade { get; private set; }
    public string? Unidade { get; private set; }
    public decimal? PrecoUnitario { get; private set; }
    public decimal? Desconto { get; private set; }
    public decimal? Total { get; private set; }
    public Guid? ProdutoId { get; private set; }
    public Guid? ServicoId { get; private set; }
    public string TipoItem { get; private set; } = CompraTipoItem.Produto;
    public string? NcmSh { get; private set; }
    public string? Csosn { get; private set; }
    public string? Cfop { get; private set; }
    public decimal? ValorLiquido { get; private set; }
    public decimal? BaseIcms { get; private set; }
    public decimal? ValorIcms { get; private set; }
    public decimal? ValorIpi { get; private set; }
    public decimal? AliqIcms { get; private set; }
    public decimal? AliqIpi { get; private set; }

    public Produto? Produto { get; private set; }
    public Servico? Servico { get; private set; }

    private CompraItem()
    {
    }

    public static CompraItem Criar(
        Guid compraId,
        int ordem,
        string descricaoOriginal,
        string? codigoImpresso,
        decimal quantidade,
        string? unidade,
        decimal? precoUnitario,
        decimal? desconto,
        decimal? total) => new()
    {
        Id = Guid.NewGuid(),
        CompraId = compraId,
        Ordem = ordem,
        DescricaoOriginal = descricaoOriginal.Trim(),
        CodigoImpresso = string.IsNullOrWhiteSpace(codigoImpresso) ? null : codigoImpresso.Trim(),
        Quantidade = quantidade,
        Unidade = string.IsNullOrWhiteSpace(unidade) ? null : unidade.Trim(),
        PrecoUnitario = precoUnitario,
        Desconto = desconto,
        Total = total
    };

    public void VincularProduto(Guid? produtoId) => ProdutoId = produtoId;

    public void VincularServico(Guid? servicoId)
    {
        ServicoId = servicoId;
        TipoItem = servicoId.HasValue ? CompraTipoItem.Servico : CompraTipoItem.Produto;
    }

    public void AplicarFiscal(
        string? tipoItem,
        string? ncmSh,
        string? csosn,
        string? cfop,
        decimal? valorLiquido,
        decimal? baseIcms,
        decimal? valorIcms,
        decimal? valorIpi,
        decimal? aliqIcms,
        decimal? aliqIpi)
    {
        TipoItem = CompraTipoItem.Normalizar(tipoItem);
        NcmSh = Limitar(ncmSh, 20);
        Csosn = Limitar(csosn, 20);
        Cfop = Limitar(cfop, 20);
        ValorLiquido = valorLiquido ?? Total;
        BaseIcms = baseIcms;
        ValorIcms = valorIcms;
        ValorIpi = valorIpi;
        AliqIcms = aliqIcms;
        AliqIpi = aliqIpi;
    }

    private static string? Limitar(string? valor, int max) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim()[..Math.Min(valor.Trim().Length, max)];

    public void AtualizarLancamento(
        string descricaoOriginal,
        string? codigoImpresso,
        decimal quantidade,
        string? unidade,
        decimal? precoUnitario,
        decimal? desconto,
        decimal? total)
    {
        DescricaoOriginal = descricaoOriginal.Trim();
        CodigoImpresso = string.IsNullOrWhiteSpace(codigoImpresso) ? null : codigoImpresso.Trim();
        Quantidade = quantidade;
        Unidade = string.IsNullOrWhiteSpace(unidade) ? null : unidade.Trim();
        PrecoUnitario = precoUnitario;
        Desconto = desconto;
        Total = total;
    }
}
