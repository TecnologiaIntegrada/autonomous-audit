using AutonomousAudit.Application;
using AutonomousAudit.Application.Data;
using AutonomousAudit.Domain;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace AutonomousAudit.Application.RequestHandlers;

public record CriarCompraCommand(Guid UsuarioId) : ICommand<CriarCompraResult>;
public record CriarCompraResult(Compra Compra);

public record ListarComprasQuery(Guid UsuarioId, string? Status, bool SomenteOrigem = false) : IQuery<ListarComprasResult>;
public record ListarComprasResult(IReadOnlyList<Compra> Compras);

public record ObterCompraQuery(Guid UsuarioId, Guid CompraId) : IQuery<ObterCompraResult>;
public abstract record ObterCompraResult
{
    public static ObterCompraResult Ok(Compra compra, IReadOnlyList<Compra> duplicatas) => new ObterCompraOk(compra, duplicatas);
    public static ObterCompraResult NotFound() => new ObterCompraNotFound();
    public static ObterCompraResult Forbidden() => new ObterCompraForbidden();
}
public record ObterCompraOk(Compra Compra, IReadOnlyList<Compra> Duplicatas) : ObterCompraResult;
public record ObterCompraNotFound : ObterCompraResult;
public record ObterCompraForbidden : ObterCompraResult;

public record AnexarCompraCommand(
    Guid UsuarioId,
    Guid CompraId,
    string NomeArquivo,
    string Mime,
    long TamanhoBytes,
    Stream Conteudo,
    string OrigemInformada) : ICommand<AnexarCompraResult>;

public abstract record AnexarCompraResult
{
    public static AnexarCompraResult Ok(CompraAnexo anexo) => new AnexarCompraOk(anexo);
    public static AnexarCompraResult NotFound() => new AnexarCompraNotFound();
    public static AnexarCompraResult Forbidden() => new AnexarCompraForbidden();
    public static AnexarCompraResult BadRequest(string message) => new AnexarCompraBadRequest(message);
}
public record AnexarCompraOk(CompraAnexo Anexo) : AnexarCompraResult;
public record AnexarCompraNotFound : AnexarCompraResult;
public record AnexarCompraForbidden : AnexarCompraResult;
public record AnexarCompraBadRequest(string Message) : AnexarCompraResult;

public record PatchAnexosCompraCommand(
    Guid UsuarioId,
    Guid CompraId,
    IReadOnlyList<Guid>? Ordem,
    IReadOnlyList<Guid>? Remover) : ICommand<PatchAnexosCompraResult>;

public abstract record PatchAnexosCompraResult
{
    public static PatchAnexosCompraResult Ok(Compra compra) => new PatchAnexosCompraOk(compra);
    public static PatchAnexosCompraResult NotFound() => new PatchAnexosCompraNotFound();
    public static PatchAnexosCompraResult Forbidden() => new PatchAnexosCompraForbidden();
    public static PatchAnexosCompraResult BadRequest(string message) => new PatchAnexosCompraBadRequest(message);
}
public record PatchAnexosCompraOk(Compra Compra) : PatchAnexosCompraResult;
public record PatchAnexosCompraNotFound : PatchAnexosCompraResult;
public record PatchAnexosCompraForbidden : PatchAnexosCompraResult;
public record PatchAnexosCompraBadRequest(string Message) : PatchAnexosCompraResult;

public record AlterarFornecedorReciboCommand(Guid UsuarioId, Guid CompraId, Guid FornecedorId)
    : ICommand<AlterarFornecedorReciboResult>;

public abstract record AlterarFornecedorReciboResult
{
    public static AlterarFornecedorReciboResult Ok(Compra compra) => new AlterarFornecedorReciboOk(compra);
    public static AlterarFornecedorReciboResult NotFound() => new AlterarFornecedorReciboNotFound();
    public static AlterarFornecedorReciboResult Forbidden() => new AlterarFornecedorReciboForbidden();
    public static AlterarFornecedorReciboResult BadRequest(string message) => new AlterarFornecedorReciboBadRequest(message);
}
public record AlterarFornecedorReciboOk(Compra Compra) : AlterarFornecedorReciboResult;
public record AlterarFornecedorReciboNotFound : AlterarFornecedorReciboResult;
public record AlterarFornecedorReciboForbidden : AlterarFornecedorReciboResult;
public record AlterarFornecedorReciboBadRequest(string Message) : AlterarFornecedorReciboResult;

public record CriarCapturaCommand(Guid UsuarioId, Guid CompraId) : ICommand<CriarCapturaResult>;
public abstract record CriarCapturaResult
{
    public static CriarCapturaResult Ok(string url, DateTimeOffset expiraEm) => new CriarCapturaOk(url, expiraEm);
    public static CriarCapturaResult NotFound() => new CriarCapturaNotFound();
    public static CriarCapturaResult Forbidden() => new CriarCapturaForbidden();
    public static CriarCapturaResult BadRequest(string message) => new CriarCapturaBadRequest(message);
}
public record CriarCapturaOk(string Url, DateTimeOffset ExpiraEm) : CriarCapturaResult;
public record CriarCapturaNotFound : CriarCapturaResult;
public record CriarCapturaForbidden : CriarCapturaResult;
public record CriarCapturaBadRequest(string Message) : CriarCapturaResult;

public record StatusCapturaQuery(Guid UsuarioId, Guid CompraId) : IQuery<StatusCapturaResult>;
public abstract record StatusCapturaResult
{
    public static StatusCapturaResult Ok(
        bool ativa,
        bool consumida,
        DateTimeOffset? expiraEm,
        int anexos,
        bool concluida) => new StatusCapturaOk(ativa, consumida, expiraEm, anexos, concluida);
    public static StatusCapturaResult NotFound() => new StatusCapturaNotFound();
    public static StatusCapturaResult Forbidden() => new StatusCapturaForbidden();
}
public record StatusCapturaOk(bool Ativa, bool Consumida, DateTimeOffset? ExpiraEm, int Anexos, bool Concluida) : StatusCapturaResult;
public record StatusCapturaNotFound : StatusCapturaResult;
public record StatusCapturaForbidden : StatusCapturaResult;

public record AbrirCapturaCommand(string Token) : ICommand<AbrirCapturaResult>;
public abstract record AbrirCapturaResult
{
    public static AbrirCapturaResult Ok(Guid compraId, string sessionToken, DateTimeOffset expiraEm) =>
        new AbrirCapturaOk(compraId, sessionToken, expiraEm);
    public static AbrirCapturaResult BadRequest(string message) => new AbrirCapturaBadRequest(message);
}
public record AbrirCapturaOk(Guid CompraId, string SessionToken, DateTimeOffset ExpiraEm) : AbrirCapturaResult;
public record AbrirCapturaBadRequest(string Message) : AbrirCapturaResult;

public record AnexarCapturaPublicaCommand(
    string Token,
    string SessionToken,
    string NomeArquivo,
    string Mime,
    long TamanhoBytes,
    Stream Conteudo) : ICommand<AnexarCompraResult>;

public record ConcluirCapturaCommand(string Token, string SessionToken) : ICommand<ConcluirCapturaResult>;
public abstract record ConcluirCapturaResult
{
    public static ConcluirCapturaResult Ok() => new ConcluirCapturaOk();
    public static ConcluirCapturaResult BadRequest(string message) => new ConcluirCapturaBadRequest(message);
}
public record ConcluirCapturaOk : ConcluirCapturaResult;
public record ConcluirCapturaBadRequest(string Message) : ConcluirCapturaResult;

public record EnfileirarProcessamentoCommand(Guid UsuarioId, Guid CompraId, bool Reprocessar) : ICommand<EnfileirarProcessamentoResult>;
public abstract record EnfileirarProcessamentoResult
{
    public static EnfileirarProcessamentoResult Ok(Compra compra) => new EnfileirarProcessamentoOk(compra);
    public static EnfileirarProcessamentoResult NotFound() => new EnfileirarProcessamentoNotFound();
    public static EnfileirarProcessamentoResult Forbidden() => new EnfileirarProcessamentoForbidden();
    public static EnfileirarProcessamentoResult BadRequest(string message) => new EnfileirarProcessamentoBadRequest(message);
}
public record EnfileirarProcessamentoOk(Compra Compra) : EnfileirarProcessamentoResult;
public record EnfileirarProcessamentoNotFound : EnfileirarProcessamentoResult;
public record EnfileirarProcessamentoForbidden : EnfileirarProcessamentoResult;
public record EnfileirarProcessamentoBadRequest(string Message) : EnfileirarProcessamentoResult;

public record PreviewDocumentoQuery(Guid UsuarioId, Guid CompraId) : IQuery<PreviewDocumentoResult>;
public abstract record PreviewDocumentoResult
{
    public static PreviewDocumentoResult Ok(string url) => new PreviewDocumentoOk(url);
    public static PreviewDocumentoResult NotFound() => new PreviewDocumentoNotFound();
    public static PreviewDocumentoResult Forbidden() => new PreviewDocumentoForbidden();
}
public record PreviewDocumentoOk(string Url) : PreviewDocumentoResult;
public record PreviewDocumentoNotFound : PreviewDocumentoResult;
public record PreviewDocumentoForbidden : PreviewDocumentoResult;

public record ResumoComprasQuery(Guid UsuarioId, int Ano, int Mes, Guid? FornecedorId)
    : IQuery<ResumoComprasResult>;
public record ResumoComprasResult(
    ComprasResumo Resumo,
    long ArmazenamentoUsadoBytes,
    long ArmazenamentoLimiteBytes,
    long QwenTokensTotal,
    decimal QwenCustoTotalBrl,
    long PerplexityTokensTotal,
    decimal PerplexityCustoTotalBrl,
    int Ano,
    int Mes);

public record ValidarCompraCommand(
    Guid UsuarioId,
    Guid CompraId,
    DateTimeOffset? DataCompra,
    string? NumeroRecibo,
    decimal? Subtotal,
    decimal? Descontos,
    decimal? Acrescimos,
    decimal? Total,
    string? FormaPagamento,
    ValidarFornecedorBody Fornecedor,
    IReadOnlyList<ValidarItemBody> Itens) : ICommand<ValidarCompraResult>;

public record ValidarFornecedorBody(
    Guid? Id,
    string Nome,
    string? RazaoSocial,
    string? CpfCnpj,
    string? Telefone,
    string? Endereco);

public record ValidarItemBody(
    Guid? Id,
    string Descricao,
    string? Codigo,
    decimal Quantidade,
    string? Unidade,
    decimal? PrecoUnitario,
    decimal? Desconto,
    decimal? Total,
    Guid? ProdutoId,
    ValidarProdutoNovoBody? ProdutoNovo);

public record ValidarProdutoNovoBody(
    string Nome,
    string? Marca,
    string? Variante,
    string? UnidadeControle,
    decimal? ConteudoEmbalagem);

public abstract record ValidarCompraResult
{
    public static ValidarCompraResult Ok(Compra compra) => new ValidarCompraOk(compra);
    public static ValidarCompraResult NotFound() => new ValidarCompraNotFound();
    public static ValidarCompraResult Forbidden() => new ValidarCompraForbidden();
    public static ValidarCompraResult BadRequest(string message) => new ValidarCompraBadRequest(message);
}
public record ValidarCompraOk(Compra Compra) : ValidarCompraResult;
public record ValidarCompraNotFound : ValidarCompraResult;
public record ValidarCompraForbidden : ValidarCompraResult;
public record ValidarCompraBadRequest(string Message) : ValidarCompraResult;

public sealed class CriarCompraHandler : IRequestHandler<CriarCompraCommand, CriarCompraResult>
{
    private readonly ICompraRepository _compras;
    private readonly IUnitOfWork _uow;

    public CriarCompraHandler(ICompraRepository compras, IUnitOfWork uow)
    {
        _compras = compras;
        _uow = uow;
    }

    public async Task<CriarCompraResult> Handle(CriarCompraCommand request, CancellationToken cancellationToken)
    {
        var compra = Compra.CriarRascunho(request.UsuarioId);
        await _compras.AddAsync(compra, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
        return new CriarCompraResult(compra);
    }
}

public record ReciboArquivoEntrada(string NomeArquivo, string Mime, byte[] Bytes);

public record CriarReciboComAnexosCommand(Guid UsuarioId, IReadOnlyList<ReciboArquivoEntrada> Arquivos)
    : ICommand<CriarReciboComAnexosResult>;

public abstract record CriarReciboComAnexosResult
{
    public static CriarReciboComAnexosResult Ok(Compra compra) => new CriarReciboComAnexosOk(compra);
    public static CriarReciboComAnexosResult BadRequest(string message) => new CriarReciboComAnexosBadRequest(message);
}
public record CriarReciboComAnexosOk(Compra Compra) : CriarReciboComAnexosResult;
public record CriarReciboComAnexosBadRequest(string Message) : CriarReciboComAnexosResult;

public sealed class CriarReciboComAnexosHandler : IRequestHandler<CriarReciboComAnexosCommand, CriarReciboComAnexosResult>
{
    private readonly ICompraRepository _compras;
    private readonly IArquivoStaging _staging;
    private readonly IArquivoEventoPublisher _nats;
    private readonly IUnitOfWork _uow;

    public CriarReciboComAnexosHandler(
        ICompraRepository compras,
        IArquivoStaging staging,
        IArquivoEventoPublisher nats,
        IUnitOfWork uow)
    {
        _compras = compras;
        _staging = staging;
        _nats = nats;
        _uow = uow;
    }

    public async Task<CriarReciboComAnexosResult> Handle(
        CriarReciboComAnexosCommand request,
        CancellationToken cancellationToken)
    {
        if (request.Arquivos.Count == 0)
        {
            return CriarReciboComAnexosResult.BadRequest("Envie ao menos uma imagem ou um PDF.");
        }

        var compra = Compra.CriarRascunho(request.UsuarioId);
        var totalNovo = request.Arquivos.Sum(x => x.Bytes.LongLength);
        var cotaErro = ReciboAnexoRegras.ValidarCota(
            await _compras.SomarTamanhoAnexosDoUsuarioAsync(request.UsuarioId, cancellationToken),
            totalNovo);
        if (cotaErro is not null)
        {
            return CriarReciboComAnexosResult.BadRequest(cotaErro);
        }

        foreach (var arquivo in request.Arquivos)
        {
            var tamanhoErro = ReciboAnexoRegras.ValidarTamanho(arquivo.Bytes.Length);
            if (tamanhoErro is not null)
            {
                return CriarReciboComAnexosResult.BadRequest($"{arquivo.NomeArquivo}: {tamanhoErro}");
            }

            var (origem, mime) = CompraAnexoHelper.Classificar(arquivo.NomeArquivo, arquivo.Mime, "");
            if (origem is null)
            {
                return CriarReciboComAnexosResult.BadRequest(
                    $"{arquivo.NomeArquivo}: envie imagens (jpeg, png, webp, heic) ou um PDF.");
            }

            await using var buffer = new MemoryStream(arquivo.Bytes);
            var caminho = await _staging.SalvarAsync(Guid.NewGuid(), buffer, cancellationToken);
            var hash = TextoNormalizado.HashSha256Hex(arquivo.Bytes);
            compra.AdicionarAnexo(arquivo.NomeArquivo, mime, origem, hash, caminho, arquivo.Bytes.Length);
        }

        compra.MarcarProcessando();
        await _compras.AddAsync(compra, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
        await _nats.PublicarCompraProcessarAsync(new CompraProcessarEvento(compra.Id, Guid.NewGuid()), cancellationToken);
        return CriarReciboComAnexosResult.Ok(compra);
    }
}

public sealed class ListarComprasHandler : IRequestHandler<ListarComprasQuery, ListarComprasResult>
{
    private readonly ICompraRepository _compras;
    private readonly IAdministradorSistemaRepository _admins;

    public ListarComprasHandler(ICompraRepository compras, IAdministradorSistemaRepository admins)
    {
        _compras = compras;
        _admins = admins;
    }

    public async Task<ListarComprasResult> Handle(ListarComprasQuery request, CancellationToken cancellationToken)
    {
        var escopo = await ComprasEscopo.CriarAsync(_admins, request.UsuarioId, cancellationToken);
        var lista = await _compras.ListarAsync(escopo, request.Status, request.SomenteOrigem, cancellationToken);
        return new ListarComprasResult(lista);
    }
}

public sealed class ObterCompraHandler : IRequestHandler<ObterCompraQuery, ObterCompraResult>
{
    private readonly ICompraRepository _compras;
    private readonly IAdministradorSistemaRepository _admins;

    public ObterCompraHandler(ICompraRepository compras, IAdministradorSistemaRepository admins)
    {
        _compras = compras;
        _admins = admins;
    }

    public async Task<ObterCompraResult> Handle(ObterCompraQuery request, CancellationToken cancellationToken)
    {
        var escopo = await ComprasEscopo.CriarAsync(_admins, request.UsuarioId, cancellationToken);
        var compra = await _compras.GetByIdDetalheAsync(request.CompraId, cancellationToken);
        if (compra is null)
        {
            return ObterCompraResult.NotFound();
        }

        if (!ComprasEscopo.PodeAcessar(escopo, compra.UsuarioId))
        {
            return ObterCompraResult.Forbidden();
        }

        var duplicatas = await _compras.BuscarDuplicadasAsync(
            compra.UsuarioId,
            compra.Id,
            compra.HashArquivo,
            compra.ChaveDuplicidade,
            cancellationToken);
        return ObterCompraResult.Ok(compra, duplicatas);
    }
}

public sealed class AnexarCompraHandler : IRequestHandler<AnexarCompraCommand, AnexarCompraResult>
{
    private readonly ICompraRepository _compras;
    private readonly IAdministradorSistemaRepository _admins;
    private readonly IArquivoStaging _staging;
    private readonly IUnitOfWork _uow;

    public AnexarCompraHandler(
        ICompraRepository compras,
        IAdministradorSistemaRepository admins,
        IArquivoStaging staging,
        IUnitOfWork uow)
    {
        _compras = compras;
        _admins = admins;
        _staging = staging;
        _uow = uow;
    }

    public async Task<AnexarCompraResult> Handle(AnexarCompraCommand request, CancellationToken cancellationToken)
    {
        var escopo = await ComprasEscopo.CriarAsync(_admins, request.UsuarioId, cancellationToken);
        var compra = await _compras.GetByIdAsync(request.CompraId, cancellationToken);
        if (compra is null)
        {
            return AnexarCompraResult.NotFound();
        }

        if (!ComprasEscopo.PodeAcessar(escopo, compra.UsuarioId))
        {
            return AnexarCompraResult.Forbidden();
        }

        if (compra.Status is not CompraStatus.Rascunho and not CompraStatus.FalhaProcessamento)
        {
            return AnexarCompraResult.BadRequest("Anexos so podem ser alterados em rascunho ou apos falha.");
        }

        var (origem, mime) = CompraAnexoHelper.Classificar(request.NomeArquivo, request.Mime, request.OrigemInformada);
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
        var idTemp = Guid.NewGuid();
        buffer.Position = 0;
        var caminho = await _staging.SalvarAsync(idTemp, buffer, cancellationToken);
        var anexo = compra.AdicionarAnexo(request.NomeArquivo, mime, origem, hash, caminho, bytes.Length);
        await _uow.SaveChangesAsync(cancellationToken);
        return AnexarCompraResult.Ok(anexo);
    }
}

public sealed class PatchAnexosCompraHandler : IRequestHandler<PatchAnexosCompraCommand, PatchAnexosCompraResult>
{
    private readonly ICompraRepository _compras;
    private readonly IAdministradorSistemaRepository _admins;
    private readonly IArquivoStaging _staging;
    private readonly IUnitOfWork _uow;

    public PatchAnexosCompraHandler(
        ICompraRepository compras,
        IAdministradorSistemaRepository admins,
        IArquivoStaging staging,
        IUnitOfWork uow)
    {
        _compras = compras;
        _admins = admins;
        _staging = staging;
        _uow = uow;
    }

    public async Task<PatchAnexosCompraResult> Handle(PatchAnexosCompraCommand request, CancellationToken cancellationToken)
    {
        var escopo = await ComprasEscopo.CriarAsync(_admins, request.UsuarioId, cancellationToken);
        var compra = await _compras.GetByIdAsync(request.CompraId, cancellationToken);
        if (compra is null)
        {
            return PatchAnexosCompraResult.NotFound();
        }

        if (!ComprasEscopo.PodeAcessar(escopo, compra.UsuarioId))
        {
            return PatchAnexosCompraResult.Forbidden();
        }

        if (compra.Status is not CompraStatus.Rascunho and not CompraStatus.FalhaProcessamento)
        {
            return PatchAnexosCompraResult.BadRequest("Anexos so podem ser alterados em rascunho ou apos falha.");
        }

        if (request.Remover is { Count: > 0 })
        {
            foreach (var id in request.Remover)
            {
                var removido = compra.RemoverAnexo(id);
                if (removido is not null)
                {
                    _staging.Excluir(removido.CaminhoLocal);
                }
            }
        }

        if (request.Ordem is { Count: > 0 })
        {
            compra.ReordenarAnexos(request.Ordem);
        }

        await _uow.SaveChangesAsync(cancellationToken);
        return PatchAnexosCompraResult.Ok(compra);
    }
}

public sealed class AlterarFornecedorReciboHandler : IRequestHandler<AlterarFornecedorReciboCommand, AlterarFornecedorReciboResult>
{
    private readonly ICompraRepository _compras;
    private readonly IFornecedorRepository _fornecedores;
    private readonly IAdministradorSistemaRepository _admins;
    private readonly IUnitOfWork _uow;

    public AlterarFornecedorReciboHandler(
        ICompraRepository compras,
        IFornecedorRepository fornecedores,
        IAdministradorSistemaRepository admins,
        IUnitOfWork uow)
    {
        _compras = compras;
        _fornecedores = fornecedores;
        _admins = admins;
        _uow = uow;
    }

    public async Task<AlterarFornecedorReciboResult> Handle(
        AlterarFornecedorReciboCommand request,
        CancellationToken cancellationToken)
    {
        var escopo = await ComprasEscopo.CriarAsync(_admins, request.UsuarioId, cancellationToken);
        var compra = await _compras.GetByIdAsync(request.CompraId, cancellationToken);
        if (compra is null)
        {
            return AlterarFornecedorReciboResult.NotFound();
        }

        if (!ComprasEscopo.PodeAcessar(escopo, compra.UsuarioId))
        {
            return AlterarFornecedorReciboResult.Forbidden();
        }

        var fornecedor = await _fornecedores.GetByIdAsync(request.FornecedorId, cancellationToken);
        if (fornecedor is null)
        {
            return AlterarFornecedorReciboResult.BadRequest("Fornecedor ou prestador nao encontrado.");
        }

        if (fornecedor.UsuarioId != compra.UsuarioId)
        {
            return AlterarFornecedorReciboResult.BadRequest(
                "O fornecedor precisa pertencer ao mesmo usuario do recibo.");
        }

        compra.VincularFornecedor(fornecedor.Id);

        var origemId = compra.ReciboOrigemId ?? compra.Id;
        if (compra.ReciboOrigemId is Guid origemFk)
        {
            var origem = await _compras.GetByIdAsync(origemFk, cancellationToken);
            origem?.VincularFornecedor(fornecedor.Id);
        }

        foreach (var derivada in await _compras.ListarDerivadasAsync(origemId, cancellationToken))
        {
            if (derivada.Id != compra.Id)
            {
                derivada.VincularFornecedor(fornecedor.Id);
            }
        }

        await _uow.SaveChangesAsync(cancellationToken);
        var atualizada = await _compras.GetByIdDetalheAsync(compra.Id, cancellationToken) ?? compra;
        return AlterarFornecedorReciboResult.Ok(atualizada);
    }
}

public static class CompraAnexoHelper
{
    public static (string? Origem, string Mime) Classificar(string nome, string mime, string origemInformada)
    {
        var nomeLower = nome.ToLowerInvariant();
        var mimeLower = (mime ?? string.Empty).ToLowerInvariant();
        if (origemInformada == CompraAnexoOrigem.Pdf || mimeLower.Contains("pdf") || nomeLower.EndsWith(".pdf"))
        {
            return (CompraAnexoOrigem.Pdf, "application/pdf");
        }

        if (mimeLower.StartsWith("image/") || nomeLower.EndsWith(".jpg") || nomeLower.EndsWith(".jpeg")
            || nomeLower.EndsWith(".png") || nomeLower.EndsWith(".webp") || nomeLower.EndsWith(".heic"))
        {
            var mimeFinal = string.IsNullOrWhiteSpace(mimeLower) ? "image/jpeg" : mimeLower;
            var origem = origemInformada == CompraAnexoOrigem.Captura ? CompraAnexoOrigem.Captura : CompraAnexoOrigem.Imagem;
            return (origem, mimeFinal);
        }

        return (null, mimeLower);
    }
}
