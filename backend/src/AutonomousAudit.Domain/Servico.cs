namespace AutonomousAudit.Domain;

public class Servico
{
    public Guid Id { get; private set; }
    public Guid UsuarioId { get; private set; }
    public Guid? ReciboId { get; private set; }
    public string Nome { get; private set; } = string.Empty;
    public string NomeNormalizado { get; private set; } = string.Empty;
    public string? CodigoExterno { get; private set; }
    public string UnidadeControle { get; private set; } = "un";
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

    private Servico()
    {
    }

    public static Servico Criar(
        Guid usuarioId,
        string nome,
        string nomeNormalizado,
        string? unidadeControle,
        Guid? reciboId = null)
    {
        var agora = DateTimeOffset.UtcNow;
        return new Servico
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuarioId,
            ReciboId = reciboId,
            Nome = nome.Trim(),
            NomeNormalizado = nomeNormalizado,
            UnidadeControle = string.IsNullOrWhiteSpace(unidadeControle) ? "un" : unidadeControle.Trim(),
            DataCriacao = agora,
            DataAtualizacao = agora
        };
    }

    public void Atualizar(string nome, string nomeNormalizado, string? unidadeControle)
    {
        Nome = nome.Trim();
        NomeNormalizado = nomeNormalizado;
        UnidadeControle = string.IsNullOrWhiteSpace(unidadeControle) ? UnidadeControle : unidadeControle.Trim();
        DataAtualizacao = DateTimeOffset.UtcNow;
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
        CodigoExterno = Texto(codigoExterno, 80);
        NcmSh = Texto(ncmSh, 20);
        Csosn = Texto(csosn, 20);
        Cfop = Texto(cfop, 20);
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

    public void VincularRecibo(Guid reciboId) => DefinirRecibo(reciboId);

    public void DefinirRecibo(Guid? reciboId)
    {
        ReciboId = reciboId;
        DataAtualizacao = DateTimeOffset.UtcNow;
    }

    private static string? Texto(string? valor, int max) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim()[..Math.Min(valor.Trim().Length, max)];
}
