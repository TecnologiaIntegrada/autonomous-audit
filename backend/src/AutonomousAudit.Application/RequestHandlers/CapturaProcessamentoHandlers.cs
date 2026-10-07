using AutonomousAudit.Application;
using AutonomousAudit.Application.Data;
using AutonomousAudit.Domain;
using MediatR;

namespace AutonomousAudit.Application.RequestHandlers;

public sealed class CriarCapturaHandler : IRequestHandler<CriarCapturaCommand, CriarCapturaResult>
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(15);
    private readonly ICompraRepository _compras;
    private readonly IAdministradorSistemaRepository _admins;
    private readonly IFrontUrl _front;
    private readonly IUnitOfWork _uow;

    public CriarCapturaHandler(
        ICompraRepository compras,
        IAdministradorSistemaRepository admins,
        IFrontUrl front,
        IUnitOfWork uow)
    {
        _compras = compras;
        _admins = admins;
        _front = front;
        _uow = uow;
    }

    public async Task<CriarCapturaResult> Handle(CriarCapturaCommand request, CancellationToken cancellationToken)
    {
        var escopo = await ComprasEscopo.CriarAsync(_admins, request.UsuarioId, cancellationToken);
        var compra = await _compras.GetByIdAsync(request.CompraId, cancellationToken);
        if (compra is null)
        {
            return CriarCapturaResult.NotFound();
        }

        if (!ComprasEscopo.PodeAcessar(escopo, compra.UsuarioId))
        {
            return CriarCapturaResult.Forbidden();
        }

        if (compra.Status is not CompraStatus.Rascunho and not CompraStatus.FalhaProcessamento)
        {
            return CriarCapturaResult.BadRequest("Captura so e permitida em rascunho.");
        }

        var token = TextoNormalizado.TokenAleatorio();
        var sessao = CapturaSessao.Criar(compra.Id, compra.UsuarioId, TextoNormalizado.HashTexto(token), Ttl);
        compra.IniciarCaptura(sessao);
        await _uow.SaveChangesAsync(cancellationToken);
        var baseUrl = _front.BaseUrl.TrimEnd('/');
        return CriarCapturaResult.Ok($"{baseUrl}/captura/{token}", sessao.ExpiraEm);
    }
}

public sealed class StatusCapturaHandler : IRequestHandler<StatusCapturaQuery, StatusCapturaResult>
{
    private readonly ICompraRepository _compras;
    private readonly IAdministradorSistemaRepository _admins;

    public StatusCapturaHandler(ICompraRepository compras, IAdministradorSistemaRepository admins)
    {
        _compras = compras;
        _admins = admins;
    }

    public async Task<StatusCapturaResult> Handle(StatusCapturaQuery request, CancellationToken cancellationToken)
    {
        var escopo = await ComprasEscopo.CriarAsync(_admins, request.UsuarioId, cancellationToken);
        var compra = await _compras.GetByIdAsync(request.CompraId, cancellationToken);
        if (compra is null)
        {
            return StatusCapturaResult.NotFound();
        }

        if (!ComprasEscopo.PodeAcessar(escopo, compra.UsuarioId))
        {
            return StatusCapturaResult.Forbidden();
        }

        var agora = DateTimeOffset.UtcNow;
        var sessao = compra.Capturas.OrderByDescending(x => x.CriadoEm).FirstOrDefault();
        var anexosCaptura = compra.Anexos.Count(x => x.Origem == CompraAnexoOrigem.Captura);
        return StatusCapturaResult.Ok(
            sessao is not null && sessao.EstaAberta(agora),
            sessao?.ConsumidoEm is not null,
            sessao?.ExpiraEm,
            anexosCaptura,
            sessao?.EncerradoEm is not null);
    }
}

public sealed class AbrirCapturaHandler : IRequestHandler<AbrirCapturaCommand, AbrirCapturaResult>
{
    private readonly ICompraRepository _compras;
    private readonly IUnitOfWork _uow;

    public AbrirCapturaHandler(ICompraRepository compras, IUnitOfWork uow)
    {
        _compras = compras;
        _uow = uow;
    }

    public async Task<AbrirCapturaResult> Handle(AbrirCapturaCommand request, CancellationToken cancellationToken)
    {
        var hash = TextoNormalizado.HashTexto(request.Token.Trim());
        var sessao = await _compras.GetCapturaPorTokenHashAsync(hash, cancellationToken);
        if (sessao is null || !sessao.PodeAbrir(DateTimeOffset.UtcNow))
        {
            return AbrirCapturaResult.BadRequest("QR expirado ou ja utilizado. Gere um novo no computador.");
        }

        var sessionToken = TextoNormalizado.TokenAleatorio(16);
        sessao.Consumir(TextoNormalizado.HashTexto(sessionToken));
        await _uow.SaveChangesAsync(cancellationToken);
        return AbrirCapturaResult.Ok(sessao.CompraId, sessionToken, sessao.ExpiraEm);
    }
}

public sealed class AnexarCapturaPublicaHandler : IRequestHandler<AnexarCapturaPublicaCommand, AnexarCompraResult>
{
    private readonly ICompraRepository _compras;
    private readonly IArquivoStaging _staging;
    private readonly IUnitOfWork _uow;

    public AnexarCapturaPublicaHandler(ICompraRepository compras, IArquivoStaging staging, IUnitOfWork uow)
    {
        _compras = compras;
        _staging = staging;
        _uow = uow;
    }

    public async Task<AnexarCompraResult> Handle(AnexarCapturaPublicaCommand request, CancellationToken cancellationToken)
    {
        var sessao = await _compras.GetCapturaPorSessionHashAsync(
            TextoNormalizado.HashTexto(request.SessionToken),
            cancellationToken);
        if (sessao is null
            || sessao.TokenHash != TextoNormalizado.HashTexto(request.Token)
            || sessao.ConsumidoEm is null
            || !sessao.EstaAberta(DateTimeOffset.UtcNow))
        {
            return AnexarCompraResult.BadRequest("Sessao de captura invalida ou expirada.");
        }

        var compra = await _compras.GetByIdAsync(sessao.CompraId, cancellationToken);
        if (compra is null)
        {
            return AnexarCompraResult.NotFound();
        }

        if (compra.Status is not CompraStatus.Rascunho and not CompraStatus.FalhaProcessamento)
        {
            return AnexarCompraResult.BadRequest("Anexos so podem ser alterados em rascunho ou apos falha.");
        }

        var (origem, mime) = CompraAnexoHelper.Classificar(
            request.NomeArquivo,
            request.Mime,
            CompraAnexoOrigem.Captura);
        if (origem is null)
        {
            return AnexarCompraResult.BadRequest("Envie imagens (jpeg, png, webp, heic) ou um PDF.");
        }

        var tamanhoInformado = ReciboAnexoRegras.ValidarTamanho(request.TamanhoBytes);
        if (tamanhoInformado is not null)
        {
            return AnexarCompraResult.BadRequest(tamanhoInformado);
        }

        await using var buffer = new MemoryStream();
        await request.Conteudo.CopyToAsync(buffer, cancellationToken);
        var tamanhoErro = ReciboAnexoRegras.ValidarTamanho(buffer.Length);
        if (tamanhoErro is not null)
        {
            return AnexarCompraResult.BadRequest(tamanhoErro);
        }

        var bytes = buffer.ToArray();
        var cotaErro = ReciboAnexoRegras.ValidarCota(
            await _compras.SomarTamanhoAnexosDoUsuarioAsync(compra.UsuarioId, cancellationToken),
            bytes.LongLength);
        if (cotaErro is not null)
        {
            return AnexarCompraResult.BadRequest(cotaErro);
        }

        var hash = TextoNormalizado.HashSha256Hex(bytes);
        buffer.Position = 0;
        var caminho = await _staging.SalvarAsync(Guid.NewGuid(), buffer, cancellationToken);
        var anexo = compra.AdicionarAnexo(request.NomeArquivo, mime, CompraAnexoOrigem.Captura, hash, caminho, bytes.Length);
        await _uow.SaveChangesAsync(cancellationToken);
        return AnexarCompraResult.Ok(anexo);
    }
}

public sealed class ConcluirCapturaHandler : IRequestHandler<ConcluirCapturaCommand, ConcluirCapturaResult>
{
    private readonly ICompraRepository _compras;
    private readonly IUnitOfWork _uow;

    public ConcluirCapturaHandler(ICompraRepository compras, IUnitOfWork uow)
    {
        _compras = compras;
        _uow = uow;
    }

    public async Task<ConcluirCapturaResult> Handle(ConcluirCapturaCommand request, CancellationToken cancellationToken)
    {
        var sessao = await _compras.GetCapturaPorSessionHashAsync(
            TextoNormalizado.HashTexto(request.SessionToken),
            cancellationToken);
        if (sessao is null || sessao.TokenHash != TextoNormalizado.HashTexto(request.Token))
        {
            return ConcluirCapturaResult.BadRequest("Sessao de captura invalida.");
        }

        sessao.Encerrar();
        await _uow.SaveChangesAsync(cancellationToken);
        return ConcluirCapturaResult.Ok();
    }
}

public sealed class EnfileirarProcessamentoHandler : IRequestHandler<EnfileirarProcessamentoCommand, EnfileirarProcessamentoResult>
{
    private readonly ICompraRepository _compras;
    private readonly IAdministradorSistemaRepository _admins;
    private readonly IArquivoEventoPublisher _nats;
    private readonly IUnitOfWork _uow;

    public EnfileirarProcessamentoHandler(
        ICompraRepository compras,
        IAdministradorSistemaRepository admins,
        IArquivoEventoPublisher nats,
        IUnitOfWork uow)
    {
        _compras = compras;
        _admins = admins;
        _nats = nats;
        _uow = uow;
    }

    public async Task<EnfileirarProcessamentoResult> Handle(
        EnfileirarProcessamentoCommand request,
        CancellationToken cancellationToken)
    {
        var escopo = await ComprasEscopo.CriarAsync(_admins, request.UsuarioId, cancellationToken);
        var compra = await _compras.GetByIdAsync(request.CompraId, cancellationToken);
        if (compra is null)
        {
            return EnfileirarProcessamentoResult.NotFound();
        }

        if (!ComprasEscopo.PodeAcessar(escopo, compra.UsuarioId))
        {
            return EnfileirarProcessamentoResult.Forbidden();
        }

        if (compra.Anexos.Count == 0)
        {
            return EnfileirarProcessamentoResult.BadRequest("Inclua ao menos um anexo antes de processar.");
        }

        if (!request.Reprocessar && compra.Status == CompraStatus.Processando)
        {
            await _nats.PublicarCompraProcessarAsync(
                new CompraProcessarEvento(compra.Id, Guid.NewGuid()),
                cancellationToken);
            return EnfileirarProcessamentoResult.Ok(compra);
        }

        var permitido = request.Reprocessar
            ? compra.Status is CompraStatus.Processando or CompraStatus.Processado or CompraStatus.Revisao or CompraStatus.FalhaProcessamento
            : compra.Status is CompraStatus.Rascunho or CompraStatus.FalhaProcessamento;

        if (!permitido)
        {
            return EnfileirarProcessamentoResult.BadRequest(
                request.Reprocessar
                    ? "Reprocessar so e permitido em revisao, processamento ou falha."
                    : "Processe a partir do rascunho ou de uma falha.");
        }

        if (request.Reprocessar)
        {
            compra.ReiniciarProcessamento();
        }
        else
        {
            compra.MarcarProcessando();
        }
        await _uow.SaveChangesAsync(cancellationToken);
        await _nats.PublicarCompraProcessarAsync(
            new CompraProcessarEvento(compra.Id, Guid.NewGuid()),
            cancellationToken);
        return EnfileirarProcessamentoResult.Ok(compra);
    }
}

public sealed class PreviewDocumentoHandler : IRequestHandler<PreviewDocumentoQuery, PreviewDocumentoResult>
{
    private readonly ICompraRepository _compras;
    private readonly IAdministradorSistemaRepository _admins;
    private readonly IDropboxDocumentos _dropbox;

    public PreviewDocumentoHandler(
        ICompraRepository compras,
        IAdministradorSistemaRepository admins,
        IDropboxDocumentos dropbox)
    {
        _compras = compras;
        _admins = admins;
        _dropbox = dropbox;
    }

    public async Task<PreviewDocumentoResult> Handle(PreviewDocumentoQuery request, CancellationToken cancellationToken)
    {
        var escopo = await ComprasEscopo.CriarAsync(_admins, request.UsuarioId, cancellationToken);
        var compra = await _compras.GetByIdDetalheAsync(request.CompraId, cancellationToken);
        if (compra is null)
        {
            return PreviewDocumentoResult.NotFound();
        }

        if (!ComprasEscopo.PodeAcessar(escopo, compra.UsuarioId))
        {
            return PreviewDocumentoResult.Forbidden();
        }

        var documento = compra.Documentos.OrderByDescending(x => x.DataAtualizacao).FirstOrDefault();
        if (documento is null || string.IsNullOrWhiteSpace(documento.DropboxPath))
        {
            return PreviewDocumentoResult.NotFound();
        }

        try
        {
            var url = await _dropbox.ObterLinkTemporarioAsync(documento.DropboxPath, cancellationToken);
            return PreviewDocumentoResult.Ok(url);
        }
        catch (DropboxStorageException)
        {
            return PreviewDocumentoResult.NotFound();
        }
    }
}

public sealed class ResumoComprasHandler : IRequestHandler<ResumoComprasQuery, ResumoComprasResult>
{
    private readonly ICompraRepository _compras;
    private readonly IAdministradorSistemaRepository _admins;
    private readonly IUsuarioPerplexityUsoMensalRepository _perplexityMensal;

    public ResumoComprasHandler(
        ICompraRepository compras,
        IAdministradorSistemaRepository admins,
        IUsuarioPerplexityUsoMensalRepository perplexityMensal)
    {
        _compras = compras;
        _admins = admins;
        _perplexityMensal = perplexityMensal;
    }

    public async Task<ResumoComprasResult> Handle(ResumoComprasQuery request, CancellationToken cancellationToken)
    {
        var ano = request.Ano;
        var mes = request.Mes;
        if (mes is < 1 or > 12)
        {
            (ano, mes) = PeriodoBrasil.MesVigente();
        }

        var (inicio, fim) = PeriodoBrasil.IntervaloMes(ano, mes);
        var escopo = await ComprasEscopo.CriarAsync(_admins, request.UsuarioId, cancellationToken);
        var resumo = await _compras.ResumoAsync(
            escopo,
            inicio,
            fim,
            request.FornecedorId,
            cancellationToken);
        var usado = await _compras.SomarTamanhoAnexosDoUsuarioAsync(request.UsuarioId, cancellationToken);
        var perplexity = await _perplexityMensal.ObterAsync(request.UsuarioId, ano, mes, cancellationToken);
        return new ResumoComprasResult(
            resumo,
            usado,
            ReciboAnexoRegras.LimiteUsuarioBytes,
            0,
            0,
            perplexity?.Tokens ?? 0,
            perplexity?.CustoBrl ?? 0,
            ano,
            mes);
    }
}

public record BaixarDocumentoQuery(Guid CompraId, Guid? UsuarioId = null, Guid? ArquivoId = null)
    : IQuery<BaixarDocumentoResult>;
public abstract record BaixarDocumentoResult
{
    public static BaixarDocumentoResult Ok(byte[] bytes, string nomeArquivo) => new BaixarDocumentoOk(bytes, nomeArquivo);
    public static BaixarDocumentoResult NotFound() => new BaixarDocumentoNotFound();
    public static BaixarDocumentoResult Forbidden() => new BaixarDocumentoForbidden();
}
public record BaixarDocumentoOk(byte[] Bytes, string NomeArquivo) : BaixarDocumentoResult;
public record BaixarDocumentoNotFound : BaixarDocumentoResult;
public record BaixarDocumentoForbidden : BaixarDocumentoResult;

public sealed class BaixarDocumentoHandler : IRequestHandler<BaixarDocumentoQuery, BaixarDocumentoResult>
{
    private readonly ICompraRepository _compras;
    private readonly IDropboxDocumentos _dropbox;
    private readonly ICompraDropboxPaths _paths;

    public BaixarDocumentoHandler(
        ICompraRepository compras,
        IDropboxDocumentos dropbox,
        ICompraDropboxPaths paths)
    {
        _compras = compras;
        _dropbox = dropbox;
        _paths = paths;
    }

    public async Task<BaixarDocumentoResult> Handle(BaixarDocumentoQuery request, CancellationToken cancellationToken)
    {
        var compra = await _compras.GetByIdDetalheAsync(request.CompraId, cancellationToken);
        if (compra is null)
        {
            return BaixarDocumentoResult.NotFound();
        }

        if (request.UsuarioId is Guid usuarioEsperado && compra.UsuarioId != usuarioEsperado)
        {
            return BaixarDocumentoResult.NotFound();
        }

        var caminhos = request.ArquivoId is Guid arquivoId
            ? ReciboArquivoCaminhos.CandidatosArquivo(compra, arquivoId, _paths)
            : ReciboArquivoCaminhos.Candidatos(compra, _paths);

        foreach (var caminho in caminhos)
        {
            try
            {
                var bytes = await _dropbox.BaixarAsync(caminho, cancellationToken);
                if (bytes.Length > 0)
                {
                    var nome = request.ArquivoId is Guid idArquivo
                        ? $"{idArquivo:D}.pdf"
                        : $"recibo-{compra.Id:N}.pdf";
                    return BaixarDocumentoResult.Ok(bytes, nome);
                }
            }
            catch (DropboxStorageException)
            {
            }
        }

        return BaixarDocumentoResult.NotFound();
    }
}

public record LimparRascunhosAnterioresCommand(Guid UsuarioId) : ICommand<LimparRascunhosAnterioresResult>;
public record LimparRascunhosAnterioresResult(int Removidos);

public sealed class LimparRascunhosAnterioresHandler
    : IRequestHandler<LimparRascunhosAnterioresCommand, LimparRascunhosAnterioresResult>
{
    private readonly ICompraRepository _compras;
    private readonly IMediator _mediator;

    public LimparRascunhosAnterioresHandler(ICompraRepository compras, IMediator mediator)
    {
        _compras = compras;
        _mediator = mediator;
    }

    public async Task<LimparRascunhosAnterioresResult> Handle(
        LimparRascunhosAnterioresCommand request,
        CancellationToken cancellationToken)
    {
        var ids = await _compras.ListarIdsRascunhosAnterioresAoDiaAsync(
            request.UsuarioId,
            PeriodoBrasil.InicioDoDiaAtual(),
            cancellationToken);
        var removidos = 0;
        foreach (var id in ids)
        {
            var resultado = await _mediator.Send(new ExcluirReciboCommand(request.UsuarioId, id), cancellationToken);
            if (resultado is ExcluirReciboOk)
            {
                removidos++;
            }
        }

        return new LimparRascunhosAnterioresResult(removidos);
    }
}

public record ExcluirReciboCommand(Guid UsuarioId, Guid CompraId) : ICommand<ExcluirReciboResult>;
public abstract record ExcluirReciboResult
{
    public static ExcluirReciboResult Ok() => new ExcluirReciboOk();
    public static ExcluirReciboResult NotFound() => new ExcluirReciboNotFound();
    public static ExcluirReciboResult Forbidden() => new ExcluirReciboForbidden();
    public static ExcluirReciboResult BadRequest(string message) => new ExcluirReciboBadRequest(message);
}
public record ExcluirReciboOk : ExcluirReciboResult;
public record ExcluirReciboNotFound : ExcluirReciboResult;
public record ExcluirReciboForbidden : ExcluirReciboResult;
public record ExcluirReciboBadRequest(string Message) : ExcluirReciboResult;

public sealed class ExcluirReciboHandler : IRequestHandler<ExcluirReciboCommand, ExcluirReciboResult>
{
    private readonly ICompraRepository _compras;
    private readonly IFornecedorRepository _fornecedores;
    private readonly IProdutoRepository _produtos;
    private readonly IServicoRepository _servicos;
    private readonly IAdministradorSistemaRepository _admins;
    private readonly IDropboxDocumentos _dropbox;
    private readonly ICompraDropboxPaths _paths;
    private readonly IArquivoStaging _staging;
    private readonly IUnitOfWork _uow;

    public ExcluirReciboHandler(
        ICompraRepository compras,
        IFornecedorRepository fornecedores,
        IProdutoRepository produtos,
        IServicoRepository servicos,
        IAdministradorSistemaRepository admins,
        IDropboxDocumentos dropbox,
        ICompraDropboxPaths paths,
        IArquivoStaging staging,
        IUnitOfWork uow)
    {
        _compras = compras;
        _fornecedores = fornecedores;
        _produtos = produtos;
        _servicos = servicos;
        _admins = admins;
        _dropbox = dropbox;
        _paths = paths;
        _staging = staging;
        _uow = uow;
    }

    public async Task<ExcluirReciboResult> Handle(ExcluirReciboCommand request, CancellationToken cancellationToken)
    {
        var escopo = await ComprasEscopo.CriarAsync(_admins, request.UsuarioId, cancellationToken);
        var compra = await _compras.GetByIdDetalheAsync(request.CompraId, cancellationToken);
        if (compra is null)
        {
            return ExcluirReciboResult.NotFound();
        }

        if (!ComprasEscopo.PodeAcessar(escopo, compra.UsuarioId))
        {
            return ExcluirReciboResult.Forbidden();
        }

        var lote = new List<Compra> { compra };
        if (compra.ReciboOrigemId is null)
        {
            lote.AddRange(await _compras.ListarDerivadasAsync(compra.Id, cancellationToken));
        }

        var produtoIds = new HashSet<Guid>();
        var servicoIds = new HashSet<Guid>();
        var fornecedorIds = new HashSet<Guid>();
        foreach (var item in lote)
        {
            if (item.FornecedorId is Guid fornecedorId)
            {
                fornecedorIds.Add(fornecedorId);
            }

            foreach (var linha in item.Itens)
            {
                if (linha.ProdutoId is Guid produtoId)
                {
                    produtoIds.Add(produtoId);
                }

                if (linha.ServicoId is Guid servicoId)
                {
                    servicoIds.Add(servicoId);
                }
            }

            foreach (var produto in await _produtos.ListarAsync(escopo, null, item.Id, cancellationToken))
            {
                produtoIds.Add(produto.Id);
            }

            foreach (var servico in await _servicos.ListarAsync(escopo, null, item.Id, cancellationToken))
            {
                servicoIds.Add(servico.Id);
            }

            await LimparArquivosAsync(item, cancellationToken);
        }

        foreach (var derivada in lote.Where(x => x.Id != compra.Id))
        {
            await _compras.RemoveAsync(derivada, cancellationToken);
        }

        await _compras.RemoveAsync(compra, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        foreach (var produtoId in produtoIds)
        {
            if (await _produtos.ContarItensVinculadosAsync(produtoId, cancellationToken) > 0)
            {
                continue;
            }

            var produto = await _produtos.GetByIdAsync(produtoId, cancellationToken);
            if (produto is not null)
            {
                _produtos.Remove(produto);
            }
        }

        foreach (var servicoId in servicoIds)
        {
            if (await _servicos.ContarItensVinculadosAsync(servicoId, cancellationToken) > 0)
            {
                continue;
            }

            var servico = await _servicos.GetByIdAsync(servicoId, cancellationToken);
            if (servico is not null)
            {
                _servicos.Remove(servico);
            }
        }

        foreach (var fornecedorId in fornecedorIds)
        {
            if (await _fornecedores.ContarComprasVinculadasAsync(fornecedorId, cancellationToken) > 0)
            {
                continue;
            }

            var fornecedor = await _fornecedores.GetByIdAsync(fornecedorId, cancellationToken);
            if (fornecedor is not null)
            {
                _fornecedores.Remove(fornecedor);
            }
        }

        await _uow.SaveChangesAsync(cancellationToken);
        return ExcluirReciboResult.Ok();
    }

    private async Task LimparArquivosAsync(Compra compra, CancellationToken cancellationToken)
    {
        foreach (var caminho in ReciboArquivoCaminhos.Candidatos(compra, _paths))
        {
            try
            {
                await _dropbox.ExcluirAsync(caminho, cancellationToken);
            }
            catch (DropboxStorageException)
            {
            }
        }

        foreach (var anexo in compra.Anexos)
        {
            _staging.Excluir(anexo.CaminhoLocal);
        }
    }
}

public static class ReciboArquivoCaminhos
{
    public static IEnumerable<string> Candidatos(Compra compra, ICompraDropboxPaths paths)
    {
        var vistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var documento = compra.Documentos.OrderByDescending(x => x.DataAtualizacao).FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(documento?.DropboxPath))
        {
            vistos.Add(documento.DropboxPath);
        }

        if (documento is not null)
        {
            vistos.Add(paths.Arquivo(compra.UsuarioId, compra.Id, documento.Id));
        }

        foreach (var legado in paths.Legados(compra.Id, compra.DataCompra, compra.DataEnvio))
        {
            vistos.Add(legado);
        }

        return vistos;
    }

    public static IEnumerable<string> CandidatosArquivo(
        Compra compra,
        Guid arquivoId,
        ICompraDropboxPaths paths)
    {
        var vistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var documento = compra.Documentos.FirstOrDefault(d => d.Id == arquivoId)
            ?? compra.Documentos.OrderByDescending(x => x.DataAtualizacao).FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(documento?.DropboxPath))
        {
            vistos.Add(documento.DropboxPath);
        }

        vistos.Add(paths.Arquivo(compra.UsuarioId, compra.Id, arquivoId));
        foreach (var legado in paths.Legados(compra.Id, compra.DataCompra, compra.DataEnvio))
        {
            vistos.Add(legado);
        }

        return vistos;
    }
}
