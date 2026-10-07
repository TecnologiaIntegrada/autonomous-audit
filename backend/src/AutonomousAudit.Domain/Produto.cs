namespace AutonomousAudit.Domain;

public class Produto
{
    public Guid Id { get; private set; }
    public Guid UsuarioId { get; private set; }
    public Guid? ReciboId { get; private set; }
    public string Nome { get; private set; } = string.Empty;
    public string NomeNormalizado { get; private set; } = string.Empty;
    public string? Marca { get; private set; }
    public string? Variante { get; private set; }
    public string UnidadeControle { get; private set; } = "un";
    public decimal? ConteudoEmbalagem { get; private set; }
    public string? CodigoExterno { get; private set; }
    public string? NcmSh { get; private set; }
    public string? Csosn { get; private set; }
    public string? Cfop { get; private set; }
    public decimal? ValorUnitario { get; private set; }
    public decimal? ValorDesconto { get; private set; }
    public decimal? ValorLiquido { get; private set; }
    public decimal? BaseIcms { get; private set; }
    public decimal? ValorIcms { get; private set; }
    public decimal? ValorIpi { get; private set; }
    public decimal? AliqIcms { get; private set; }
    public decimal? AliqIpi { get; private set; }
    public DateTimeOffset DataCriacao { get; private set; }
    public DateTimeOffset DataAtualizacao { get; private set; }

    private readonly List<ProdutoNome> _nomes = [];

    public IReadOnlyCollection<ProdutoNome> Nomes => _nomes;

    private Produto()
    {
    }

    public static Produto Criar(
        Guid usuarioId,
        string nome,
        string nomeNormalizado,
        string? marca,
        string? variante,
        string unidadeControle,
        decimal? conteudoEmbalagem,
        Guid? reciboId = null)
    {
        var agora = DateTimeOffset.UtcNow;
        var produto = new Produto
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuarioId,
            ReciboId = reciboId,
            Nome = nome.Trim(),
            NomeNormalizado = nomeNormalizado,
            Marca = TextoOpcional(marca),
            Variante = TextoOpcional(variante),
            UnidadeControle = string.IsNullOrWhiteSpace(unidadeControle) ? "un" : unidadeControle.Trim(),
            ConteudoEmbalagem = conteudoEmbalagem,
            DataCriacao = agora,
            DataAtualizacao = agora
        };
        produto.AdicionarSinonimo(nome, nomeNormalizado);
        return produto;
    }

    public void Atualizar(
        string nome,
        string nomeNormalizado,
        string? marca,
        string? variante,
        string unidadeControle,
        decimal? conteudoEmbalagem)
    {
        Nome = nome.Trim();
        NomeNormalizado = nomeNormalizado;
        Marca = TextoOpcional(marca);
        Variante = TextoOpcional(variante);
        UnidadeControle = string.IsNullOrWhiteSpace(unidadeControle) ? UnidadeControle : unidadeControle.Trim();
        ConteudoEmbalagem = conteudoEmbalagem;
        DataAtualizacao = DateTimeOffset.UtcNow;
        AdicionarSinonimo(nome, nomeNormalizado);
    }

    public void AtualizarFiscal(
        string? codigoExterno,
        string? ncmSh,
        string? csosn,
        string? cfop,
        decimal? valorUnitario,
        decimal? valorDesconto,
        decimal? valorLiquido,
        decimal? baseIcms,
        decimal? valorIcms,
        decimal? valorIpi,
        decimal? aliqIcms,
        decimal? aliqIpi)
    {
        CodigoExterno = TextoOpcional(codigoExterno, 80);
        NcmSh = TextoOpcional(ncmSh, 20);
        Csosn = TextoOpcional(csosn, 20);
        Cfop = TextoOpcional(cfop, 20);
        ValorUnitario = valorUnitario;
        ValorDesconto = valorDesconto;
        ValorLiquido = valorLiquido;
        BaseIcms = baseIcms;
        ValorIcms = valorIcms;
        ValorIpi = valorIpi;
        AliqIcms = aliqIcms;
        AliqIpi = aliqIpi;
        DataAtualizacao = DateTimeOffset.UtcNow;
    }

    public void AdicionarSinonimo(string nome, string nomeNormalizado)
    {
        if (string.IsNullOrWhiteSpace(nomeNormalizado))
        {
            return;
        }

        if (_nomes.Any(x => x.NomeNormalizado == nomeNormalizado))
        {
            return;
        }

        _nomes.Add(ProdutoNome.Criar(Id, nome.Trim(), nomeNormalizado));
        DataAtualizacao = DateTimeOffset.UtcNow;
    }

    public void SubstituirSinonimos(IReadOnlyList<(string Nome, string NomeNormalizado)> nomes)
    {
        var desejados = nomes
            .Select(n => (Nome: (n.Nome ?? string.Empty).Trim(), Norm: (n.NomeNormalizado ?? string.Empty).Trim()))
            .Where(x => x.Nome.Length > 0 && x.Norm.Length > 0)
            .GroupBy(x => x.Norm)
            .Select(g => g.First())
            .ToList();

        var nomeAtual = Nome.Trim();
        if (nomeAtual.Length > 0 && desejados.All(d => !string.Equals(d.Nome, nomeAtual, StringComparison.OrdinalIgnoreCase)))
        {
            desejados.Insert(0, (nomeAtual, NomeNormalizado));
        }

        foreach (var atual in _nomes.ToList())
        {
            if (desejados.All(d => d.Norm != atual.NomeNormalizado))
            {
                _nomes.Remove(atual);
            }
        }

        foreach (var (nome, norm) in desejados)
        {
            if (_nomes.Any(x => x.NomeNormalizado == norm))
            {
                continue;
            }

            _nomes.Add(ProdutoNome.Criar(Id, nome, norm));
        }

        DataAtualizacao = DateTimeOffset.UtcNow;
    }

    public void VincularRecibo(Guid reciboId) => DefinirRecibo(reciboId);

    public void DefinirRecibo(Guid? reciboId)
    {
        ReciboId = reciboId;
        DataAtualizacao = DateTimeOffset.UtcNow;
    }

    private static string? TextoOpcional(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    private static string? TextoOpcional(string? valor, int max) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim()[..Math.Min(valor.Trim().Length, max)];
}
