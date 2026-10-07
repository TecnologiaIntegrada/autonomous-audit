using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using AutonomousAudit.Application;
using AutonomousAudit.Application.Data;
using AutonomousAudit.Domain;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AutonomousAudit.Application.RequestHandlers;

public record ProcessarCompraJobCommand(Guid CompraId) : ICommand<ProcessarCompraJobResult>;

public abstract record ProcessarCompraJobResult
{
    public static ProcessarCompraJobResult Ok() => new ProcessarCompraJobOk();
    public static ProcessarCompraJobResult Retry(string message) => new ProcessarCompraJobRetry(message);
    public static ProcessarCompraJobResult Ignorado(string message) => new ProcessarCompraJobIgnorado(message);
}

public record ProcessarCompraJobOk : ProcessarCompraJobResult;
public record ProcessarCompraJobRetry(string Message) : ProcessarCompraJobResult;
public record ProcessarCompraJobIgnorado(string Message) : ProcessarCompraJobResult;

public sealed class ProcessarCompraJobHandler : IRequestHandler<ProcessarCompraJobCommand, ProcessarCompraJobResult>
{
    private const int MaxTentativas = 2;
    private const int RetryDelaySeconds = 2;
    public const int TimeoutJobSeconds = 420;

    private readonly ICompraRepository _compras;
    private readonly IFornecedorRepository _fornecedores;
    private readonly IProdutoRepository _produtos;
    private readonly IServicoRepository _servicos;
    private readonly IReciboPdfMontador _pdf;
    private readonly IDropboxDocumentos _dropbox;
    private readonly ICompraDropboxPaths _paths;
    private readonly IPerplexityClient _perplexity;
    private readonly IRegistrarPerplexityUsoUsuario _perplexityUso;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<ProcessarCompraJobHandler> _logger;

    public ProcessarCompraJobHandler(
        ICompraRepository compras,
        IFornecedorRepository fornecedores,
        IProdutoRepository produtos,
        IServicoRepository servicos,
        IReciboPdfMontador pdf,
        IDropboxDocumentos dropbox,
        ICompraDropboxPaths paths,
        IPerplexityClient perplexity,
        IRegistrarPerplexityUsoUsuario perplexityUso,
        IUnitOfWork uow,
        ILogger<ProcessarCompraJobHandler> logger)
    {
        _compras = compras;
        _fornecedores = fornecedores;
        _produtos = produtos;
        _servicos = servicos;
        _pdf = pdf;
        _dropbox = dropbox;
        _paths = paths;
        _perplexity = perplexity;
        _perplexityUso = perplexityUso;
        _uow = uow;
        _logger = logger;
    }

    public async Task<ProcessarCompraJobResult> Handle(ProcessarCompraJobCommand request, CancellationToken cancellationToken)
    {
        var compra = await _compras.GetByIdDetalheAsync(request.CompraId, cancellationToken);
        if (compra is null)
        {
            _logger.LogWarning(
                "OCR recibo ignorado CompraId={CompraId} Motivo={Motivo}",
                request.CompraId,
                "Compra nao encontrada.");
            return ProcessarCompraJobResult.Ignorado("Compra nao encontrada.");
        }

        using var contexto = _logger.BeginScope(new Dictionary<string, object>
        {
            ["CompraId"] = compra.Id,
            ["UsuarioId"] = compra.UsuarioId,
            ["Status"] = compra.Status.ToString(),
            ["Anexos"] = compra.Anexos.Count,
            ["Tentativas"] = compra.TentativasProcessamento,
            ["CodigoCurto"] = CodigoCurto(compra.Id)
        });

        if (compra.Status is CompraStatus.Processado or CompraStatus.Revisao or CompraStatus.Validada or CompraStatus.Concluida)
        {
            var extracaoIncompleta = string.IsNullOrWhiteSpace(compra.QwenJsonBruto)
                && compra.Itens.Count == 0
                && compra.FornecedorId is null;
            if (!extracaoIncompleta)
            {
                _logger.LogInformation(
                    "OCR recibo ja processado CompraId={CompraId} Status={Status}",
                    compra.Id,
                    compra.Status);
                return ProcessarCompraJobResult.Ok();
            }

            compra.MarcarProcessando();
        }

        if (compra.Status is not CompraStatus.Processando and not CompraStatus.FalhaProcessamento)
        {
            _logger.LogWarning(
                "OCR recibo ignorado CompraId={CompraId} Status={Status} Motivo={Motivo}",
                compra.Id,
                compra.Status,
                "Status nao processa.");
            return ProcessarCompraJobResult.Ignorado($"Status {compra.Status} nao processa.");
        }

        var bytesAnexos = BytesAnexos(compra);
        _logger.LogInformation(
            "OCR recibo iniciado CompraId={CompraId} UsuarioId={UsuarioId} Status={Status} Anexos={Anexos} BytesAnexos={BytesAnexos} Tentativas={Tentativas} TimeoutSeconds={TimeoutSeconds} CodigoCurto={CodigoCurto}",
            compra.Id,
            compra.UsuarioId,
            compra.Status,
            compra.Anexos.Count,
            bytesAnexos,
            compra.TentativasProcessamento,
            TimeoutJobSeconds,
            CodigoCurto(compra.Id));

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(TimeoutJobSeconds));
        var cronometro = Stopwatch.StartNew();
        try
        {
            await ExecutarAsync(compra, timeoutCts.Token);
            await _uow.SaveChangesAsync(cancellationToken);
            _logger.LogInformation(
                "OCR recibo concluido CompraId={CompraId} Status={Status} DuracaoMs={DuracaoMs} Itens={Itens}",
                compra.Id,
                compra.Status,
                cronometro.ElapsedMilliseconds,
                compra.Itens.Count);
            return ProcessarCompraJobResult.Ok();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return await FalharSemRetryAsync(
                compra,
                $"Timeout de {TimeoutJobSeconds}s no processamento do recibo.",
                cronometro,
                cancellationToken);
        }
        catch (TimeoutException ex)
        {
            return await FalharSemRetryAsync(compra, ex.Message, cronometro, cancellationToken);
        }
        catch (Exception ex)
        {
            using (LogExcecao.IniciarEscopo(
                       _logger,
                       ex,
                       ("CompraId", compra.Id),
                       ("UsuarioId", compra.UsuarioId),
                       ("Anexos", compra.Anexos.Count),
                       ("Tentativas", compra.TentativasProcessamento),
                       ("DuracaoMs", cronometro.ElapsedMilliseconds),
                       ("CodigoCurto", CodigoCurto(compra.Id))))
            {
                _logger.LogError(
                    ex,
                    "Falha ao processar compra CompraId={CompraId} UsuarioId={UsuarioId} Anexos={Anexos} Tentativas={Tentativas} DuracaoMs={DuracaoMs} tipo={Tipo} cadeia={Cadeia} CodigoCurto={CodigoCurto}",
                    compra.Id,
                    compra.UsuarioId,
                    compra.Anexos.Count,
                    compra.TentativasProcessamento,
                    cronometro.ElapsedMilliseconds,
                    ex.GetType().Name,
                    LogExcecao.Cadeia(ex),
                    CodigoCurto(compra.Id));
            }

            compra.RegistrarTentativa(ex.Message);
            if (compra.TentativasProcessamento >= MaxTentativas)
            {
                compra.MarcarFalha(ex.Message);
                await PersistirErroAsync(compra.Id, cancellationToken);
                return ProcessarCompraJobResult.Ignorado(ex.Message);
            }

            compra.MarcarProcessando();
            await PersistirErroAsync(compra.Id, cancellationToken);
            await Task.Delay(TimeSpan.FromSeconds(RetryDelaySeconds), cancellationToken);
            return ProcessarCompraJobResult.Retry(ex.Message);
        }
    }

    private async Task ExecutarAsync(Compra compra, CancellationToken cancellationToken)
    {
        if (compra.Anexos.Count == 0)
        {
            throw new InvalidOperationException("A compra nao tem anexos.");
        }

        _logger.LogInformation("OCR etapa={Etapa} CompraId={CompraId}", "ResolverPdf", compra.Id);
        var documento = compra.Documentos.OrderBy(x => x.Ordem).LastOrDefault();
        var (pdfBytes, origem, hash, veioDoDropbox) = await ResolverPdfAsync(compra, documento, cancellationToken);
        _logger.LogInformation(
            "OCR etapa={Etapa} CompraId={CompraId} origem={Origem} pdfBytes={PdfBytes} veioDoDropbox={VeioDoDropbox} hash={Hash}",
            "ResolverPdfOk",
            compra.Id,
            origem,
            pdfBytes.Length,
            veioDoDropbox,
            hash);
        if (documento is null)
        {
            documento = CompraDocumento.Criar(
                compra.Id,
                string.Empty,
                origem,
                hash,
                null);
            var caminhoNovo = _paths.Arquivo(compra.UsuarioId, compra.Id, documento.Id);
            documento.RegistrarDropbox(caminhoNovo, null);
            compra.RegistrarDocumento(documento);
            _logger.LogInformation(
                "OCR etapa={Etapa} CompraId={CompraId} DocumentoId={DocumentoId}",
                "DocumentoCriado",
                compra.Id,
                documento.Id);
        }

        var destinoCanonico = _paths.Arquivo(compra.UsuarioId, compra.Id, documento.Id);
        if (!veioDoDropbox || string.IsNullOrWhiteSpace(documento.DropboxId))
        {
            var destino = string.IsNullOrWhiteSpace(documento.DropboxPath)
                ? destinoCanonico
                : documento.DropboxPath;
            if (!veioDoDropbox)
            {
                _logger.LogInformation(
                    "OCR etapa={Etapa} CompraId={CompraId} destino={Destino} pdfBytes={PdfBytes}",
                    "DropboxEnviar",
                    compra.Id,
                    destino,
                    pdfBytes.Length);
                await using var envio = new MemoryStream(pdfBytes);
                var enviado = await _dropbox.EnviarAsync(destino, envio, cancellationToken);
                documento.RegistrarDropbox(enviado.Caminho, enviado.Id);
                _logger.LogInformation(
                    "OCR etapa={Etapa} CompraId={CompraId} dropboxPath={DropboxPath}",
                    "DropboxEnviarOk",
                    compra.Id,
                    enviado.Caminho);
            }
        }

        _logger.LogInformation(
            "OCR etapa={Etapa} CompraId={CompraId} pdfBytes={PdfBytes} forcarImagens={ForcarImagens}",
            "PerplexityAnalisar",
            compra.Id,
            pdfBytes.Length,
            true);
        var agente = await _perplexity.AnalisarAsync(
            new PerplexityAgentePedido(ReciboQwenPrompt.Texto, null, pdfBytes, "application/pdf", ForcarImagens: true),
            cancellationToken);
        await _perplexityUso.RegistrarAsync(compra.UsuarioId, agente, cancellationToken);
        _logger.LogInformation(
            "OCR etapa={Etapa} CompraId={CompraId} modelo={Modelo} textoChars={TextoChars} tokens={Tokens}",
            "PerplexityAnalisarOk",
            compra.Id,
            agente.Modelo,
            agente.Texto?.Length ?? 0,
            agente.Uso?.TotalTokens ?? 0);

        var lote = ReciboQwenParser.TentarParsearLote(agente.Texto, out var jsonLimpo).ToList();
        var jsonInvalido = lote.Count == 0;
        if (jsonInvalido)
        {
            lote.Add(new ReciboQwenDto());
            if (string.IsNullOrWhiteSpace(jsonLimpo))
            {
                jsonLimpo = agente.Texto;
            }

            _logger.LogWarning(
                "OCR etapa={Etapa} CompraId={CompraId} textoChars={TextoChars}",
                "JsonInvalido",
                compra.Id,
                agente.Texto?.Length ?? 0);
        }
        else
        {
            _logger.LogInformation(
                "OCR etapa={Etapa} CompraId={CompraId} recibos={Recibos}",
                "LoteParseado",
                compra.Id,
                lote.Count);
        }

        foreach (var derivada in await _compras.ListarDerivadasAsync(compra.Id, cancellationToken))
        {
            await _compras.RemoveAsync(derivada, cancellationToken);
        }

        await LancarReciboAsync(compra, lote[0], jsonLimpo, hash, cancellationToken, jsonInvalido);

        var irmas = new List<Compra>();
        for (var i = 1; i < lote.Count; i++)
        {
            var irma = Compra.CriarDerivada(compra);
            var docIrma = CompraDocumento.Criar(
                irma.Id,
                documento.DropboxPath,
                documento.Origem,
                documento.HashSha256,
                documento.CaminhoLocalPdf);
            docIrma.RegistrarDropbox(documento.DropboxPath, documento.DropboxId);
            irma.RegistrarDocumento(docIrma);
            await _compras.AddAsync(irma, cancellationToken);
            await LancarReciboAsync(irma, lote[i], JsonSerializer.Serialize(lote[i]), hash, cancellationToken);
            irmas.Add(irma);
        }

        var destinoFinal = _paths.Arquivo(compra.UsuarioId, compra.Id, documento.Id);
        if (!string.Equals(documento.DropboxPath, destinoFinal, StringComparison.OrdinalIgnoreCase))
        {
            var movido = await _dropbox.MoverAsync(documento.DropboxPath, destinoFinal, cancellationToken);
            documento.MoverPara(movido.Caminho);
            if (!string.IsNullOrWhiteSpace(movido.Id))
            {
                documento.RegistrarDropbox(movido.Caminho, movido.Id);
            }

            foreach (var irma in irmas)
            {
                var doc = irma.Documentos.OrderBy(x => x.Ordem).LastOrDefault();
                doc?.RegistrarDropbox(documento.DropboxPath, documento.DropboxId);
            }
        }
    }

    private async Task LancarReciboAsync(
        Compra compra,
        ReciboQwenDto dto,
        string jsonLimpo,
        string hash,
        CancellationToken cancellationToken,
        bool jsonInvalido = false)
    {
        var alertas = new List<string>();
        if (jsonInvalido)
        {
            alertas.Add("Perplexity nao devolveu JSON valido.");
        }

        var validacao = new ReciboQwenDtoValidator().Validate(dto);
        if (!validacao.IsValid)
        {
            alertas.AddRange(validacao.Errors.Select(e => e.ErrorMessage));
        }

        var dataCompra = ParseData(dto.Compra?.Data);
        var somaItens = dto.Itens.Sum(i => i.Total ?? 0);
        if (dto.Compra?.Total is decimal total && dto.Itens.Count > 0 && Math.Abs(somaItens - total) > 0.05m)
        {
            alertas.Add($"Soma dos itens ({somaItens:0.00}) diverge do total ({total:0.00}).");
        }

        var cnpj = TextoNormalizado.Digitos(dto.Fornecedor?.CpfCnpj);
        if (string.IsNullOrWhiteSpace(dto.Fornecedor?.Nome) && string.IsNullOrWhiteSpace(cnpj))
        {
            alertas.Add("Fornecedor nao identificado com seguranca.");
        }

        var fornecedor = await GarantirFornecedorAsync(compra, dto.Fornecedor, cnpj, cancellationToken);
        compra.VincularFornecedor(fornecedor.Id);

        var itens = new List<CompraItem>();
        var tiposItem = new List<string>();
        var ordem = 1;
        foreach (var extraido in dto.Itens)
        {
            var descricao = string.IsNullOrWhiteSpace(extraido.Descricao) ? $"Item {ordem}" : extraido.Descricao;
            var tipoItem = CompraTipoItem.Normalizar(extraido.Tipo ?? dto.TipoItem);
            tiposItem.Add(tipoItem);

            var item = CompraItem.Criar(
                compra.Id,
                ordem,
                descricao,
                extraido.Codigo,
                extraido.Quantidade ?? 1,
                extraido.Unidade,
                extraido.PrecoUnitario,
                extraido.Desconto,
                extraido.Total);
            item.AplicarFiscal(
                tipoItem,
                extraido.NcmSh,
                extraido.Csosn,
                extraido.Cfop,
                extraido.ValorLiquido,
                extraido.BaseIcms,
                extraido.ValorIcms,
                extraido.ValorIpi,
                extraido.AliqIcms,
                extraido.AliqIpi);

            if (CompraTipoItem.EhServico(tipoItem))
            {
                var servico = await CriarServicoDoReciboAsync(compra, extraido, descricao, cancellationToken);
                item.VincularServico(servico?.Id);
            }
            else
            {
                var produto = await CriarProdutoDoReciboAsync(
                    compra,
                    descricao,
                    extraido.Marca,
                    extraido.Variante,
                    extraido.Unidade,
                    extraido,
                    cancellationToken);
                item.VincularProduto(produto?.Id);
            }

            itens.Add(item);
            ordem++;
        }

        var tipoDocumento = ResolverTipoDocumento(dto);
        var tipoItemCompra = CompraTipoItem.Predominante(tiposItem.Count > 0 ? tiposItem : [dto.TipoItem ?? CompraTipoItem.Produto]);
        var chaveAcesso = Digitos(dto.Compra?.ChaveAcesso);
        var chave = !string.IsNullOrWhiteSpace(chaveAcesso) && chaveAcesso.Length == 44
            ? chaveAcesso
            : ComprasEscopo.ChaveDuplicidade(
                dto.Fornecedor?.CpfCnpj,
                dto.Fornecedor?.Nome,
                dto.Compra?.NumeroReciboResolvido,
                dataCompra,
                dto.Compra?.Total);
        var duplicatas = await _compras.BuscarDuplicadasAsync(compra.UsuarioId, compra.Id, hash, chave, cancellationToken);
        if (duplicatas.Count > 0)
        {
            alertas.Add($"Possivel duplicidade: {duplicatas.Count} compra(s) com o mesmo arquivo ou chave de recibo.");
        }

        var divergencias = JsonSerializer.Serialize(new
        {
            prompt = ReciboQwenPrompt.Versao,
            paginas = dto.PaginasResolvidas,
            alertas,
            fornecedorExtraido = dto.Fornecedor,
            duplicatas = duplicatas.Select(d => new { d.Id, d.NumeroRecibo, d.Total, d.DataCompra, d.Status })
        });

        compra.AplicarExtracao(
            string.IsNullOrWhiteSpace(jsonLimpo) ? "{}" : jsonLimpo,
            divergencias,
            hash,
            Limitar(dto.Compra?.NumeroReciboResolvido, 80),
            dataCompra,
            dto.Compra?.Subtotal,
            dto.Compra?.Descontos,
            dto.Compra?.Acrescimos,
            dto.Compra?.Total,
            Limitar(dto.Compra?.FormaPagamento, 80),
            string.IsNullOrWhiteSpace(chave) ? null :             chave,
            itens);
        compra.DefinirPaginas(ReciboQwenParser.PaginasTexto(dto.PaginasResolvidas));
        compra.ClassificarDocumento(
            tipoDocumento,
            tipoItemCompra,
            dto.Compra?.DanfeTipo,
            dto.Compra?.Serie,
            dto.Compra?.Folha,
            chaveAcesso,
            dto.Compra?.CodigoBarras,
            dto.Compra?.ProtocoloAutorizacao,
            ParseData(dto.Compra?.ProtocoloData),
            dto.Compra?.NaturezaOperacao,
            dto.Compra?.InscricaoEstadual ?? dto.Fornecedor?.InscricaoEstadual,
            dto.Compra?.InscricaoEstadualSt,
            ParseData(dto.Compra?.DataEmissao) ?? dataCompra);
        AplicarNfe(compra, tipoDocumento, dto.Nfe);
    }

    private async Task<(byte[] Pdf, string Origem, string Hash, bool VeioDoDropbox)> ResolverPdfAsync(
        Compra compra,
        CompraDocumento? documento,
        CancellationToken cancellationToken)
    {
        var baixado = await TentarBaixarDropboxAsync(compra, documento, cancellationToken);
        if (baixado is not null)
        {
            var origemDropbox = documento?.Origem ?? CompraAnexoOrigem.Pdf;
            var hashDropbox = !string.IsNullOrWhiteSpace(documento?.HashSha256)
                ? documento.HashSha256
                : TextoNormalizado.HashSha256Hex(baixado);
            return (baixado, origemDropbox, hashDropbox, true);
        }

        if (!compra.Anexos.Any(a => File.Exists(a.CaminhoLocal)))
        {
            throw new InvalidOperationException(
                "Arquivo do recibo nao encontrado no armazenamento. Reenvie o recibo.");
        }

        var (pdf, origem) = await _pdf.MontarAsync(compra.Anexos.ToList(), cancellationToken);
        _logger.LogInformation(
            "OCR etapa={Etapa} CompraId={CompraId} origem={Origem} pdfBytes={PdfBytes} anexos={Anexos}",
            "MontarPdfOk",
            compra.Id,
            origem,
            pdf.Length,
            compra.Anexos.Count);
        return (pdf, origem, TextoNormalizado.HashSha256Hex(pdf), false);
    }

    private async Task<byte[]?> TentarBaixarDropboxAsync(
        Compra compra,
        CompraDocumento? documento,
        CancellationToken cancellationToken)
    {
        var caminhos = new List<string>();
        if (!string.IsNullOrWhiteSpace(documento?.DropboxPath))
        {
            caminhos.Add(documento.DropboxPath);
        }

        if (documento is not null)
        {
            caminhos.Add(_paths.Arquivo(compra.UsuarioId, compra.Id, documento.Id));
        }

        caminhos.AddRange(_paths.Legados(compra.Id, compra.DataCompra, compra.DataEnvio));

        foreach (var caminho in caminhos.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var bytes = await _dropbox.BaixarAsync(caminho, cancellationToken);
                _logger.LogInformation("Recibo {CompraId} lido do Dropbox em {Caminho}", compra.Id, caminho);
                return bytes;
            }
            catch (DropboxStorageException ex) when (ex.Kind == DropboxErrorKind.NotFound)
            {
                _logger.LogInformation("Recibo {CompraId} ausente no Dropbox em {Caminho}", compra.Id, caminho);
            }
        }

        return null;
    }

    private async Task<Fornecedor> GarantirFornecedorAsync(
        Compra compra,
        ReciboQwenFornecedorDto? extraido,
        string? cnpj,
        CancellationToken cancellationToken) =>
        await FornecedorGarantia.ResolverAsync(
            _fornecedores,
            compra.UsuarioId,
            extraido?.Nome,
            extraido?.RazaoSocial,
            cnpj,
            extraido?.Telefone,
            extraido?.Endereco,
            cancellationToken);

    private async Task<Produto?> CriarProdutoDoReciboAsync(
        Compra compra,
        string descricao,
        string? marca,
        string? variante,
        string? unidade,
        ReciboQwenItemDto extraido,
        CancellationToken cancellationToken)
    {
        var produto = Produto.Criar(
            compra.UsuarioId,
            descricao,
            TextoNormalizado.Nome(descricao),
            marca,
            variante,
            unidade ?? "un",
            null,
            compra.Id);
        produto.AtualizarFiscal(
            extraido.Codigo,
            extraido.NcmSh,
            extraido.Csosn,
            extraido.Cfop,
            extraido.PrecoUnitario,
            extraido.Desconto,
            extraido.ValorLiquido ?? extraido.Total,
            extraido.BaseIcms,
            extraido.ValorIcms,
            extraido.ValorIpi,
            extraido.AliqIcms,
            extraido.AliqIpi);
        await _produtos.AddAsync(produto, cancellationToken);
        return produto;
    }

    private async Task<Servico?> CriarServicoDoReciboAsync(
        Compra compra,
        ReciboQwenItemDto extraido,
        string descricao,
        CancellationToken cancellationToken)
    {
        var servico = Servico.Criar(
            compra.UsuarioId,
            descricao,
            TextoNormalizado.Nome(descricao),
            extraido.Unidade,
            compra.Id);
        servico.AtualizarFiscal(
            extraido.Codigo,
            extraido.NcmSh,
            extraido.Csosn,
            extraido.Cfop,
            extraido.PrecoUnitario,
            extraido.Desconto,
            extraido.ValorLiquido ?? extraido.Total,
            extraido.BaseIcms,
            extraido.ValorIcms,
            extraido.ValorIpi,
            extraido.AliqIcms,
            extraido.AliqIpi);
        await _servicos.AddAsync(servico, cancellationToken);
        return servico;
    }

    private static string ResolverTipoDocumento(ReciboQwenDto dto)
    {
        var chave = Digitos(dto.Compra?.ChaveAcesso);
        if (chave is { Length: 44 } || dto.Nfe is not null || dto.Compra?.DanfeTipo is 0 or 1)
        {
            return CompraTipoDocumento.NotaFiscal;
        }

        return CompraTipoDocumento.Normalizar(dto.TipoDocumento);
    }

    private static void AplicarNfe(Compra compra, string tipoDocumento, ReciboQwenNfeDto? nfe)
    {
        if (!CompraTipoDocumento.EhNotaFiscal(tipoDocumento) || nfe is null)
        {
            if (!CompraTipoDocumento.EhNotaFiscal(tipoDocumento))
            {
                compra.LimparNfe();
            }

            return;
        }

        if (nfe.Destinatario is { } dest)
        {
            compra.GarantirNfeDestinatario().Atualizar(
                dest.NomeRazaoSocial,
                dest.CpfCnpj,
                dest.Endereco,
                dest.Bairro,
                dest.Cep,
                dest.Municipio,
                dest.Uf,
                dest.Telefone,
                dest.InscricaoEstadual,
                ParseData(dest.DataEmissao),
                ParseData(dest.DataSaida),
                dest.HoraSaida);
        }

        if (nfe.Imposto is { } imposto)
        {
            compra.GarantirNfeImposto().Atualizar(
                imposto.BaseIcms,
                imposto.ValorIcms,
                imposto.BaseIcmsSt,
                imposto.ValorIcmsSt,
                imposto.ValorTotalProdutos,
                imposto.ValorFrete,
                imposto.ValorSeguro,
                imposto.Desconto,
                imposto.OutrasDespesas,
                imposto.ValorIpi,
                imposto.ValorTotalNota);
        }

        if (nfe.Transportador is { } transp)
        {
            compra.GarantirNfeTransportador().Atualizar(
                transp.NomeRazaoSocial,
                transp.FretePorConta,
                transp.CodigoAntt,
                transp.Placa,
                transp.Uf,
                transp.CpfCnpj,
                transp.Endereco,
                transp.Municipio,
                transp.UfEndereco,
                transp.InscricaoEstadual,
                transp.QuantidadeVolumes,
                transp.Especie,
                transp.Marca,
                transp.Numeracao,
                transp.PesoBruto,
                transp.PesoLiquido);
        }

        if (nfe.Adicionais is { } adic)
        {
            compra.GarantirNfeAdicionais().Atualizar(
                adic.InformacoesComplementares,
                adic.ReservadoAoFisco,
                ParseData(adic.DataHoraImpressao));
        }
    }

    private static string? Digitos(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : new string(valor.Where(char.IsDigit).ToArray());

    private async Task<ProcessarCompraJobResult> FalharSemRetryAsync(
        Compra compra,
        string mensagem,
        Stopwatch cronometro,
        CancellationToken cancellationToken)
    {
        _logger.LogError(
            "OCR recibo falhou sem retry CompraId={CompraId} UsuarioId={UsuarioId} Anexos={Anexos} Tentativas={Tentativas} DuracaoMs={DuracaoMs} Motivo={Motivo} CodigoCurto={CodigoCurto}",
            compra.Id,
            compra.UsuarioId,
            compra.Anexos.Count,
            compra.TentativasProcessamento,
            cronometro.ElapsedMilliseconds,
            mensagem,
            CodigoCurto(compra.Id));
        compra.RegistrarTentativa(mensagem);
        compra.MarcarFalha(mensagem);
        await PersistirErroAsync(compra.Id, cancellationToken);
        return ProcessarCompraJobResult.Ignorado(mensagem);
    }

    private async Task PersistirErroAsync(Guid compraId, CancellationToken cancellationToken)
    {
        try
        {
            await _uow.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Falha ao persistir status de erro CompraId={CompraId} tipo={Tipo} cadeia={Cadeia}",
                compraId,
                ex.GetType().Name,
                LogExcecao.Cadeia(ex));
        }
    }

    private static long BytesAnexos(Compra compra)
    {
        long total = 0;
        foreach (var anexo in compra.Anexos)
        {
            try
            {
                if (File.Exists(anexo.CaminhoLocal))
                {
                    total += new FileInfo(anexo.CaminhoLocal).Length;
                }
            }
            catch
            {
            }
        }

        return total;
    }

    private static string CodigoCurto(Guid id) => id.ToString("N")[..8].ToUpperInvariant();

    private static DateTimeOffset? ParseData(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return null;
        }

        if (DateTimeOffset.TryParse(valor, CultureInfo.GetCultureInfo("pt-BR"), DateTimeStyles.AssumeUniversal, out var br))
        {
            return br.ToUniversalTime();
        }

        if (DateTimeOffset.TryParse(valor, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var inv))
        {
            return inv.ToUniversalTime();
        }

        return null;
    }

    private static string? Limitar(string? valor, int maximo)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return null;
        }

        var texto = valor.Trim();
        return texto.Length <= maximo ? texto : texto[..maximo];
    }
}
