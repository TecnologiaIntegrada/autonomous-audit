using AutonomousAudit.Domain;

namespace AutonomousAudit.Application.Data;

public sealed record EscopoDono(Guid UsuarioId, bool EhAdministrador);

public interface ICompraRepository
{
    Task AddAsync(Compra compra, CancellationToken cancellationToken = default);
    Task<Compra?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Compra?> GetByIdDetalheAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Compra>> ListarAsync(EscopoDono escopo, string? status, bool somenteOrigem = false, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Compra>> ListarPorFornecedorAsync(EscopoDono escopo, Guid fornecedorId, CancellationToken cancellationToken = default);
    Task<CapturaSessao?> GetCapturaPorTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);
    Task<CapturaSessao?> GetCapturaPorSessionHashAsync(string sessionHash, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Compra>> BuscarDuplicadasAsync(
        Guid usuarioId,
        Guid compraId,
        string? hashArquivo,
        string? chaveDuplicidade,
        CancellationToken cancellationToken = default);
    Task<ComprasResumo> ResumoAsync(
        EscopoDono escopo,
        DateTimeOffset inicio,
        DateTimeOffset fim,
        Guid? fornecedorId,
        CancellationToken cancellationToken = default);
    Task RemoveAsync(Compra compra, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Guid>> ListarIdsRascunhosAnterioresAoDiaAsync(
        Guid usuarioId,
        DateTimeOffset inicioDoDia,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Compra>> ListarDerivadasAsync(Guid reciboOrigemId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RelatorioItemLinha>> RelatorioItensAsync(
        EscopoDono escopo,
        DateTimeOffset inicio,
        DateTimeOffset fim,
        Guid? fornecedorId,
        int limite,
        CancellationToken cancellationToken = default);
    Task<long> SomarTamanhoAnexosDoUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default);
}

public interface IFornecedorRepository
{
    Task AddAsync(Fornecedor fornecedor, CancellationToken cancellationToken = default);
    Task<Fornecedor?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Fornecedor>> ListarAsync(EscopoDono escopo, string? busca, Guid? reciboId, CancellationToken cancellationToken = default);
    Task<Fornecedor?> BuscarPorCpfCnpjAsync(Guid usuarioId, string cpfCnpj, CancellationToken cancellationToken = default);
    Task<Fornecedor?> BuscarPorNomeNormalizadoAsync(Guid usuarioId, string nomeNormalizado, CancellationToken cancellationToken = default);
    Task<Fornecedor?> BuscarPorNomeAproximadoAsync(Guid usuarioId, string nomeNormalizado, CancellationToken cancellationToken = default);
    Task<int> ContarComprasVinculadasAsync(Guid fornecedorId, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<Guid, int>> ContarRecibosAsync(IReadOnlyCollection<Guid> fornecedorIds, CancellationToken cancellationToken = default);
    void Remove(Fornecedor fornecedor);
}

public interface IProdutoRepository
{
    Task AddAsync(Produto produto, CancellationToken cancellationToken = default);
    Task<Produto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Produto>> ListarAsync(EscopoDono escopo, string? busca, Guid? reciboId, CancellationToken cancellationToken = default);
    Task<Produto?> BuscarPorNomeOuSinonimoAsync(Guid usuarioId, string nomeNormalizado, CancellationToken cancellationToken = default);
    Task<int> ContarItensVinculadosAsync(Guid produtoId, CancellationToken cancellationToken = default);
    void Remove(Produto produto);
}

public interface IServicoRepository
{
    Task AddAsync(Servico servico, CancellationToken cancellationToken = default);
    Task<Servico?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Servico>> ListarAsync(EscopoDono escopo, string? busca, Guid? reciboId, CancellationToken cancellationToken = default);
    Task<int> ContarItensVinculadosAsync(Guid servicoId, CancellationToken cancellationToken = default);
    void Remove(Servico servico);
}

public sealed record ComprasResumo(
    int TotalCompras,
    int Rascunhos,
    int Processando,
    int EmRevisao,
    int Validadas,
    int Concluidas,
    int Falhas,
    decimal TotalGasto,
    IReadOnlyList<ComprasResumoFornecedor> PorFornecedor,
    int Processados);

public sealed record ComprasResumoFornecedor(Guid? FornecedorId, string Nome, int Quantidade, decimal Total);

public sealed class RelatorioItemLinha
{
    public Guid UsuarioId { get; init; }
    public Guid ReciboId { get; init; }
    public Guid CompraId { get; init; }
    public Guid? ReciboOrigemId { get; init; }
    public Guid? ArquivoId { get; init; }
    public DateTimeOffset Data { get; init; }
    public string Status { get; init; } = string.Empty;
    public string? NumeroRecibo { get; init; }
    public decimal? Subtotal { get; init; }
    public decimal? Descontos { get; init; }
    public decimal? Acrescimos { get; init; }
    public decimal? TotalCompra { get; init; }
    public string? FormaPagamento { get; init; }
    public string TipoDocumento { get; init; } = "recibo";
    public string TipoItemCompra { get; init; } = "produto";
    public int? DanfeTipo { get; init; }
    public string? Serie { get; init; }
    public string? Folha { get; init; }
    public string? ChaveAcesso { get; init; }
    public string? CodigoBarras { get; init; }
    public string? ProtocoloAutorizacao { get; init; }
    public DateTimeOffset? ProtocoloData { get; init; }
    public string? NaturezaOperacao { get; init; }
    public string? InscricaoEstadual { get; init; }
    public string? InscricaoEstadualSt { get; init; }
    public DateTimeOffset? DataEmissao { get; init; }
    public Guid? FornecedorId { get; init; }
    public string? FornecedorNome { get; init; }
    public string? FornecedorRazaoSocial { get; init; }
    public string? FornecedorCpfCnpj { get; init; }
    public string? FornecedorTelefone { get; init; }
    public string? FornecedorEndereco { get; init; }
    public Guid? ProdutoId { get; init; }
    public string? ProdutoNome { get; init; }
    public string? ProdutoMarca { get; init; }
    public string? ProdutoVariante { get; init; }
    public decimal? ConteudoEmbalagem { get; init; }
    public string? ProdutoCodigoExterno { get; init; }
    public string? ProdutoNcmSh { get; init; }
    public string? ProdutoCsosn { get; init; }
    public string? ProdutoCfop { get; init; }
    public decimal? ProdutoValorUnitario { get; init; }
    public decimal? ProdutoValorDesconto { get; init; }
    public decimal? ProdutoValorLiquido { get; init; }
    public decimal? ProdutoBaseIcms { get; init; }
    public decimal? ProdutoValorIcms { get; init; }
    public decimal? ProdutoValorIpi { get; init; }
    public decimal? ProdutoAliqIcms { get; init; }
    public decimal? ProdutoAliqIpi { get; init; }
    public Guid? ServicoId { get; init; }
    public string? ServicoNome { get; init; }
    public string? ServicoCodigoExterno { get; init; }
    public string? ServicoUnidadeControle { get; init; }
    public string? ServicoNcmSh { get; init; }
    public string? ServicoCsosn { get; init; }
    public string? ServicoCfop { get; init; }
    public decimal? ServicoValorUnitario { get; init; }
    public decimal? ServicoValorDesconto { get; init; }
    public decimal? ServicoValorLiquido { get; init; }
    public decimal? ServicoBaseIcms { get; init; }
    public decimal? ServicoValorIcms { get; init; }
    public decimal? ServicoValorIpi { get; init; }
    public decimal? ServicoAliqIcms { get; init; }
    public decimal? ServicoAliqIpi { get; init; }
    public string? Codigo { get; init; }
    public string Descricao { get; init; } = string.Empty;
    public decimal Quantidade { get; init; }
    public string? Unidade { get; init; }
    public decimal? PrecoUnitario { get; init; }
    public decimal? Desconto { get; init; }
    public decimal? Total { get; init; }
    public string TipoItem { get; init; } = "produto";
    public string? ItemNcmSh { get; init; }
    public string? ItemCsosn { get; init; }
    public string? ItemCfop { get; init; }
    public decimal? ItemValorLiquido { get; init; }
    public decimal? ItemBaseIcms { get; init; }
    public decimal? ItemValorIcms { get; init; }
    public decimal? ItemValorIpi { get; init; }
    public decimal? ItemAliqIcms { get; init; }
    public decimal? ItemAliqIpi { get; init; }
    public string? DestinatarioNome { get; init; }
    public string? DestinatarioCpfCnpj { get; init; }
    public string? DestinatarioEndereco { get; init; }
    public string? DestinatarioBairro { get; init; }
    public string? DestinatarioCep { get; init; }
    public string? DestinatarioMunicipio { get; init; }
    public string? DestinatarioUf { get; init; }
    public string? DestinatarioTelefone { get; init; }
    public string? DestinatarioIe { get; init; }
    public DateTimeOffset? DestinatarioDataEmissao { get; init; }
    public DateTimeOffset? DestinatarioDataSaida { get; init; }
    public string? DestinatarioHoraSaida { get; init; }
    public decimal? ImpostoBaseIcms { get; init; }
    public decimal? ImpostoValorIcms { get; init; }
    public decimal? ImpostoBaseIcmsSt { get; init; }
    public decimal? ImpostoValorIcmsSt { get; init; }
    public decimal? ImpostoValorTotalProdutos { get; init; }
    public decimal? ImpostoValorFrete { get; init; }
    public decimal? ImpostoValorSeguro { get; init; }
    public decimal? ImpostoDesconto { get; init; }
    public decimal? ImpostoOutrasDespesas { get; init; }
    public decimal? ImpostoValorIpi { get; init; }
    public decimal? ImpostoValorTotalNota { get; init; }
    public string? TransportadorNome { get; init; }
    public string? TransportadorFretePorConta { get; init; }
    public string? TransportadorCodigoAntt { get; init; }
    public string? TransportadorPlaca { get; init; }
    public string? TransportadorUf { get; init; }
    public string? TransportadorCpfCnpj { get; init; }
    public string? TransportadorEndereco { get; init; }
    public string? TransportadorMunicipio { get; init; }
    public string? TransportadorUfEndereco { get; init; }
    public string? TransportadorIe { get; init; }
    public decimal? TransportadorQuantidadeVolumes { get; init; }
    public string? TransportadorEspecie { get; init; }
    public string? TransportadorMarca { get; init; }
    public string? TransportadorNumeracao { get; init; }
    public decimal? TransportadorPesoBruto { get; init; }
    public decimal? TransportadorPesoLiquido { get; init; }
    public string? AdicionaisInformacoes { get; init; }
    public string? AdicionaisReservadoFisco { get; init; }
    public DateTimeOffset? AdicionaisDataHoraImpressao { get; init; }
}
