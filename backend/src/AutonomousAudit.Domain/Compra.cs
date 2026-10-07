namespace AutonomousAudit.Domain;

public class Compra
{
    public Guid Id { get; private set; }
    public Guid UsuarioId { get; private set; }
    public Guid? FornecedorId { get; private set; }
    public Guid? ReciboOrigemId { get; private set; }
    public string? Paginas { get; private set; }
    public DateTimeOffset DataEnvio { get; private set; }
    public DateTimeOffset? DataCompra { get; private set; }
    public string? NumeroRecibo { get; private set; }
    public string TipoDocumento { get; private set; } = CompraTipoDocumento.Recibo;
    public string TipoItem { get; private set; } = CompraTipoItem.Produto;
    public int? DanfeTipo { get; private set; }
    public string? Serie { get; private set; }
    public string? Folha { get; private set; }
    public string? ChaveAcesso { get; private set; }
    public string? CodigoBarras { get; private set; }
    public string? ProtocoloAutorizacao { get; private set; }
    public DateTimeOffset? ProtocoloData { get; private set; }
    public string? NaturezaOperacao { get; private set; }
    public string? InscricaoEstadual { get; private set; }
    public string? InscricaoEstadualSt { get; private set; }
    public DateTimeOffset? DataEmissao { get; private set; }
    public decimal? Subtotal { get; private set; }
    public decimal? Descontos { get; private set; }
    public decimal? Acrescimos { get; private set; }
    public decimal? Total { get; private set; }
    public string? FormaPagamento { get; private set; }
    public string Status { get; private set; } = CompraStatus.Rascunho;
    public string? HashArquivo { get; private set; }
    public string? QwenJsonBruto { get; private set; }
    public string? DivergenciasJson { get; private set; }
    public string? ChaveDuplicidade { get; private set; }
    public int TentativasProcessamento { get; private set; }
    public string? UltimoErro { get; private set; }
    public DateTimeOffset DataAtualizacao { get; private set; }
    public DateTimeOffset? ProcessadoEm { get; private set; }
    public DateTimeOffset? ValidadoEm { get; private set; }
    public DateTimeOffset? ConcluidoEm { get; private set; }

    public Fornecedor? Fornecedor { get; private set; }
    public CompraNfeDestinatario? NfeDestinatario { get; private set; }
    public CompraNfeImposto? NfeImposto { get; private set; }
    public CompraNfeTransportador? NfeTransportador { get; private set; }
    public CompraNfeAdicionais? NfeAdicionais { get; private set; }

    private readonly List<CompraItem> _itens = [];
    private readonly List<CompraAnexo> _anexos = [];
    private readonly List<CompraDocumento> _documentos = [];
    private readonly List<CapturaSessao> _capturas = [];

    public IReadOnlyCollection<CompraItem> Itens => _itens;
    public IReadOnlyCollection<CompraAnexo> Anexos => _anexos;
    public IReadOnlyCollection<CompraDocumento> Documentos => _documentos;
    public IReadOnlyCollection<CapturaSessao> Capturas => _capturas;

    private Compra()
    {
    }

    public static Compra CriarRascunho(Guid usuarioId) => new()
    {
        Id = Guid.NewGuid(),
        UsuarioId = usuarioId,
        DataEnvio = DateTimeOffset.UtcNow,
        DataAtualizacao = DateTimeOffset.UtcNow,
        Status = CompraStatus.Rascunho
    };

    public static Compra CriarDerivada(Compra origem) => new()
    {
        Id = Guid.NewGuid(),
        UsuarioId = origem.UsuarioId,
        ReciboOrigemId = origem.Id,
        DataEnvio = origem.DataEnvio,
        DataAtualizacao = DateTimeOffset.UtcNow,
        HashArquivo = origem.HashArquivo,
        Status = CompraStatus.Processando
    };

    public Guid ReciboArquivoId => ReciboOrigemId ?? Id;

    public void DefinirPaginas(string? paginas)
    {
        Paginas = string.IsNullOrWhiteSpace(paginas) ? null : paginas.Trim();
        DataAtualizacao = DateTimeOffset.UtcNow;
    }

    public CompraAnexo AdicionarAnexo(
        string nomeArquivo,
        string mime,
        string origem,
        string hashSha256,
        string caminhoLocal,
        long tamanhoBytes)
    {
        var ordem = _anexos.Count == 0 ? 1 : _anexos.Max(x => x.Ordem) + 1;
        var anexo = CompraAnexo.Criar(Id, ordem, nomeArquivo, mime, origem, hashSha256, caminhoLocal, tamanhoBytes);
        _anexos.Add(anexo);
        DataAtualizacao = DateTimeOffset.UtcNow;
        return anexo;
    }

    public void ReordenarAnexos(IReadOnlyList<Guid> ordem)
    {
        var posicao = 1;
        foreach (var id in ordem)
        {
            var anexo = _anexos.FirstOrDefault(x => x.Id == id);
            anexo?.DefinirOrdem(posicao++);
        }

        DataAtualizacao = DateTimeOffset.UtcNow;
    }

    public CompraAnexo? RemoverAnexo(Guid anexoId)
    {
        var anexo = _anexos.FirstOrDefault(x => x.Id == anexoId);
        if (anexo is null)
        {
            return null;
        }

        _anexos.Remove(anexo);
        var i = 1;
        foreach (var restante in _anexos.OrderBy(x => x.Ordem))
        {
            restante.DefinirOrdem(i++);
        }

        DataAtualizacao = DateTimeOffset.UtcNow;
        return anexo;
    }

    public void MarcarProcessando()
    {
        Status = CompraStatus.Processando;
        UltimoErro = null;
        DataAtualizacao = DateTimeOffset.UtcNow;
    }

    public void ReiniciarProcessamento()
    {
        TentativasProcessamento = 0;
        MarcarProcessando();
    }

    public void RegistrarTentativa(string? erro)
    {
        TentativasProcessamento += 1;
        UltimoErro = MensagemUsuario.OcultarProvedor(erro);
        DataAtualizacao = DateTimeOffset.UtcNow;
    }

    public void MarcarFalha(string erro)
    {
        Status = CompraStatus.FalhaProcessamento;
        UltimoErro = MensagemUsuario.OcultarProvedor(erro);
        DataAtualizacao = DateTimeOffset.UtcNow;
    }

    public void AplicarExtracao(
        string qwenJson,
        string divergenciasJson,
        string hashArquivo,
        string? numeroRecibo,
        DateTimeOffset? dataCompra,
        decimal? subtotal,
        decimal? descontos,
        decimal? acrescimos,
        decimal? total,
        string? formaPagamento,
        string? chaveDuplicidade,
        IReadOnlyList<CompraItem> itens)
    {
        QwenJsonBruto = qwenJson;
        DivergenciasJson = divergenciasJson;
        HashArquivo = hashArquivo;
        NumeroRecibo = numeroRecibo;
        DataCompra = dataCompra;
        Subtotal = subtotal;
        Descontos = descontos;
        Acrescimos = acrescimos;
        Total = total;
        FormaPagamento = formaPagamento;
        ChaveDuplicidade = chaveDuplicidade;
        Status = CompraStatus.Processado;
        ProcessadoEm = DateTimeOffset.UtcNow;
        UltimoErro = null;
        DataAtualizacao = DateTimeOffset.UtcNow;

        _itens.Clear();
        foreach (var item in itens)
        {
            _itens.Add(item);
        }
    }

    public void ClassificarDocumento(
        string tipoDocumento,
        string tipoItem,
        int? danfeTipo,
        string? serie,
        string? folha,
        string? chaveAcesso,
        string? codigoBarras,
        string? protocoloAutorizacao,
        DateTimeOffset? protocoloData,
        string? naturezaOperacao,
        string? inscricaoEstadual,
        string? inscricaoEstadualSt,
        DateTimeOffset? dataEmissao)
    {
        TipoDocumento = CompraTipoDocumento.Normalizar(tipoDocumento);
        TipoItem = CompraTipoItem.Normalizar(tipoItem);
        DanfeTipo = danfeTipo is 0 or 1 ? danfeTipo : null;
        Serie = Limitar(serie, 20);
        Folha = Limitar(folha, 20);
        ChaveAcesso = Limitar(chaveAcesso, 60);
        CodigoBarras = Limitar(codigoBarras, 80);
        ProtocoloAutorizacao = Limitar(protocoloAutorizacao, 80);
        ProtocoloData = protocoloData;
        NaturezaOperacao = Limitar(naturezaOperacao, 200);
        InscricaoEstadual = Limitar(inscricaoEstadual, 30);
        InscricaoEstadualSt = Limitar(inscricaoEstadualSt, 30);
        DataEmissao = dataEmissao;
        DataAtualizacao = DateTimeOffset.UtcNow;
    }

    public CompraNfeDestinatario GarantirNfeDestinatario() =>
        NfeDestinatario ??= CompraNfeDestinatario.Criar(Id);

    public CompraNfeImposto GarantirNfeImposto() =>
        NfeImposto ??= CompraNfeImposto.Criar(Id);

    public CompraNfeTransportador GarantirNfeTransportador() =>
        NfeTransportador ??= CompraNfeTransportador.Criar(Id);

    public CompraNfeAdicionais GarantirNfeAdicionais() =>
        NfeAdicionais ??= CompraNfeAdicionais.Criar(Id);

    public void LimparNfe()
    {
        NfeDestinatario = null;
        NfeImposto = null;
        NfeTransportador = null;
        NfeAdicionais = null;
        DataAtualizacao = DateTimeOffset.UtcNow;
    }

    private static string? Limitar(string? valor, int max) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim()[..Math.Min(valor.Trim().Length, max)];

    public void RegistrarDocumento(CompraDocumento documento)
    {
        if (_documentos.Any(x => x.Id == documento.Id))
        {
            return;
        }

        _documentos.Add(documento);
        DataAtualizacao = DateTimeOffset.UtcNow;
    }

    public void VincularFornecedor(Guid? fornecedorId)
    {
        FornecedorId = fornecedorId;
        DataAtualizacao = DateTimeOffset.UtcNow;
    }

    public void AplicarValidacao(
        Guid? fornecedorId,
        DateTimeOffset? dataCompra,
        string? numeroRecibo,
        decimal? subtotal,
        decimal? descontos,
        decimal? acrescimos,
        decimal? total,
        string? formaPagamento,
        string? chaveDuplicidade)
    {
        FornecedorId = fornecedorId;
        DataCompra = dataCompra;
        NumeroRecibo = numeroRecibo;
        Subtotal = subtotal;
        Descontos = descontos;
        Acrescimos = acrescimos;
        Total = total;
        FormaPagamento = formaPagamento;
        ChaveDuplicidade = chaveDuplicidade;
        Status = CompraStatus.Validada;
        ValidadoEm = DateTimeOffset.UtcNow;
        DataAtualizacao = DateTimeOffset.UtcNow;
    }

    public void IncluirItem(CompraItem item)
    {
        _itens.Add(item);
        DataAtualizacao = DateTimeOffset.UtcNow;
    }

    public void RemoverItem(Guid itemId)
    {
        var item = _itens.FirstOrDefault(x => x.Id == itemId);
        if (item is null)
        {
            return;
        }

        _itens.Remove(item);
        DataAtualizacao = DateTimeOffset.UtcNow;
    }

    public CapturaSessao IniciarCaptura(CapturaSessao sessao)
    {
        foreach (var anterior in _capturas.Where(x => x.EncerradoEm is null))
        {
            anterior.Encerrar();
        }

        _capturas.Add(sessao);
        DataAtualizacao = DateTimeOffset.UtcNow;
        return sessao;
    }
}
