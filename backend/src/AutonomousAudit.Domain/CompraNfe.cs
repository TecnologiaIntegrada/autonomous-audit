namespace AutonomousAudit.Domain;

public class CompraNfeDestinatario
{
    public Guid CompraId { get; private set; }
    public string? NomeRazaoSocial { get; private set; }
    public string? CpfCnpj { get; private set; }
    public string? Endereco { get; private set; }
    public string? Bairro { get; private set; }
    public string? Cep { get; private set; }
    public string? Municipio { get; private set; }
    public string? Uf { get; private set; }
    public string? Telefone { get; private set; }
    public string? InscricaoEstadual { get; private set; }
    public DateTimeOffset? DataEmissao { get; private set; }
    public DateTimeOffset? DataSaida { get; private set; }
    public string? HoraSaida { get; private set; }

    private CompraNfeDestinatario()
    {
    }

    public static CompraNfeDestinatario Criar(Guid compraId) => new() { CompraId = compraId };

    public void Atualizar(
        string? nomeRazaoSocial,
        string? cpfCnpj,
        string? endereco,
        string? bairro,
        string? cep,
        string? municipio,
        string? uf,
        string? telefone,
        string? inscricaoEstadual,
        DateTimeOffset? dataEmissao,
        DateTimeOffset? dataSaida,
        string? horaSaida)
    {
        NomeRazaoSocial = Texto(nomeRazaoSocial, 300);
        CpfCnpj = Texto(cpfCnpj, 20);
        Endereco = Texto(endereco, 500);
        Bairro = Texto(bairro, 120);
        Cep = Texto(cep, 12);
        Municipio = Texto(municipio, 120);
        Uf = Texto(uf, 2);
        Telefone = Texto(telefone, 30);
        InscricaoEstadual = Texto(inscricaoEstadual, 30);
        DataEmissao = dataEmissao;
        DataSaida = dataSaida;
        HoraSaida = Texto(horaSaida, 20);
    }

    private static string? Texto(string? valor, int max) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim()[..Math.Min(valor.Trim().Length, max)];
}

public class CompraNfeImposto
{
    public Guid CompraId { get; private set; }
    public decimal? BaseIcms { get; private set; }
    public decimal? ValorIcms { get; private set; }
    public decimal? BaseIcmsSt { get; private set; }
    public decimal? ValorIcmsSt { get; private set; }
    public decimal? ValorTotalProdutos { get; private set; }
    public decimal? ValorFrete { get; private set; }
    public decimal? ValorSeguro { get; private set; }
    public decimal? Desconto { get; private set; }
    public decimal? OutrasDespesas { get; private set; }
    public decimal? ValorIpi { get; private set; }
    public decimal? ValorTotalNota { get; private set; }

    private CompraNfeImposto()
    {
    }

    public static CompraNfeImposto Criar(Guid compraId) => new() { CompraId = compraId };

    public void Atualizar(
        decimal? baseIcms,
        decimal? valorIcms,
        decimal? baseIcmsSt,
        decimal? valorIcmsSt,
        decimal? valorTotalProdutos,
        decimal? valorFrete,
        decimal? valorSeguro,
        decimal? desconto,
        decimal? outrasDespesas,
        decimal? valorIpi,
        decimal? valorTotalNota)
    {
        BaseIcms = baseIcms;
        ValorIcms = valorIcms;
        BaseIcmsSt = baseIcmsSt;
        ValorIcmsSt = valorIcmsSt;
        ValorTotalProdutos = valorTotalProdutos;
        ValorFrete = valorFrete;
        ValorSeguro = valorSeguro;
        Desconto = desconto;
        OutrasDespesas = outrasDespesas;
        ValorIpi = valorIpi;
        ValorTotalNota = valorTotalNota;
    }
}

public class CompraNfeTransportador
{
    public Guid CompraId { get; private set; }
    public string? NomeRazaoSocial { get; private set; }
    public string? FretePorConta { get; private set; }
    public string? CodigoAntt { get; private set; }
    public string? Placa { get; private set; }
    public string? Uf { get; private set; }
    public string? CpfCnpj { get; private set; }
    public string? Endereco { get; private set; }
    public string? Municipio { get; private set; }
    public string? UfEndereco { get; private set; }
    public string? InscricaoEstadual { get; private set; }
    public decimal? QuantidadeVolumes { get; private set; }
    public string? Especie { get; private set; }
    public string? Marca { get; private set; }
    public string? Numeracao { get; private set; }
    public decimal? PesoBruto { get; private set; }
    public decimal? PesoLiquido { get; private set; }

    private CompraNfeTransportador()
    {
    }

    public static CompraNfeTransportador Criar(Guid compraId) => new() { CompraId = compraId };

    public void Atualizar(
        string? nomeRazaoSocial,
        string? fretePorConta,
        string? codigoAntt,
        string? placa,
        string? uf,
        string? cpfCnpj,
        string? endereco,
        string? municipio,
        string? ufEndereco,
        string? inscricaoEstadual,
        decimal? quantidadeVolumes,
        string? especie,
        string? marca,
        string? numeracao,
        decimal? pesoBruto,
        decimal? pesoLiquido)
    {
        NomeRazaoSocial = Texto(nomeRazaoSocial, 300);
        FretePorConta = Texto(fretePorConta, 80);
        CodigoAntt = Texto(codigoAntt, 40);
        Placa = Texto(placa, 20);
        Uf = Texto(uf, 2);
        CpfCnpj = Texto(cpfCnpj, 20);
        Endereco = Texto(endereco, 500);
        Municipio = Texto(municipio, 120);
        UfEndereco = Texto(ufEndereco, 2);
        InscricaoEstadual = Texto(inscricaoEstadual, 30);
        QuantidadeVolumes = quantidadeVolumes;
        Especie = Texto(especie, 80);
        Marca = Texto(marca, 80);
        Numeracao = Texto(numeracao, 80);
        PesoBruto = pesoBruto;
        PesoLiquido = pesoLiquido;
    }

    private static string? Texto(string? valor, int max) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim()[..Math.Min(valor.Trim().Length, max)];
}

public class CompraNfeAdicionais
{
    public Guid CompraId { get; private set; }
    public string? InformacoesComplementares { get; private set; }
    public string? ReservadoAoFisco { get; private set; }
    public DateTimeOffset? DataHoraImpressao { get; private set; }

    private CompraNfeAdicionais()
    {
    }

    public static CompraNfeAdicionais Criar(Guid compraId) => new() { CompraId = compraId };

    public void Atualizar(string? informacoesComplementares, string? reservadoAoFisco, DateTimeOffset? dataHoraImpressao)
    {
        InformacoesComplementares = Texto(informacoesComplementares, 4000);
        ReservadoAoFisco = Texto(reservadoAoFisco, 2000);
        DataHoraImpressao = dataHoraImpressao;
    }

    private static string? Texto(string? valor, int max) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim()[..Math.Min(valor.Trim().Length, max)];
}
