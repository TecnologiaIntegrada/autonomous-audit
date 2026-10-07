using AutonomousAudit.Application;
using AutonomousAudit.Application.Data;
using AutonomousAudit.Domain;
using FluentValidation;
using MediatR;

namespace AutonomousAudit.Application.RequestHandlers;

public record ListarFornecedoresQuery(Guid UsuarioId, string? Busca, Guid? ReciboId, bool SomenteProprio = false) : IQuery<ListarFornecedoresResult>;
public record ListarFornecedoresResult(IReadOnlyList<Fornecedor> Fornecedores, IReadOnlyDictionary<Guid, int> QuantidadeRecibos);
public record ListarRecibosFornecedorQuery(Guid UsuarioId, Guid FornecedorId) : IQuery<ListarRecibosFornecedorResult>;
public abstract record ListarRecibosFornecedorResult
{
    public static ListarRecibosFornecedorResult Ok(IReadOnlyList<Compra> recibos) => new ListarRecibosFornecedorOk(recibos);
    public static ListarRecibosFornecedorResult NotFound() => new ListarRecibosFornecedorNotFound();
    public static ListarRecibosFornecedorResult Forbidden() => new ListarRecibosFornecedorForbidden();
}
public record ListarRecibosFornecedorOk(IReadOnlyList<Compra> Recibos) : ListarRecibosFornecedorResult;
public record ListarRecibosFornecedorNotFound : ListarRecibosFornecedorResult;
public record ListarRecibosFornecedorForbidden : ListarRecibosFornecedorResult;
public record CriarFornecedorCommand(
    Guid UsuarioId,
    string Nome,
    string? RazaoSocial,
    string? CpfCnpj,
    string? Telefone,
    string? Endereco) : ICommand<SalvarFornecedorResult>;
public record AtualizarFornecedorCommand(
    Guid UsuarioId,
    Guid Id,
    string Nome,
    string? RazaoSocial,
    string? CpfCnpj,
    string? Telefone,
    string? Endereco) : ICommand<SalvarFornecedorResult>;
public record ExcluirFornecedorCommand(Guid UsuarioId, Guid Id) : ICommand<ExcluirCadastroResult>;

public abstract record SalvarFornecedorResult
{
    public static SalvarFornecedorResult Ok(Fornecedor fornecedor) => new SalvarFornecedorOk(fornecedor);
    public static SalvarFornecedorResult NotFound() => new SalvarFornecedorNotFound();
    public static SalvarFornecedorResult Forbidden() => new SalvarFornecedorForbidden();
    public static SalvarFornecedorResult BadRequest(string message) => new SalvarFornecedorBadRequest(message);
}
public record SalvarFornecedorOk(Fornecedor Fornecedor) : SalvarFornecedorResult;
public record SalvarFornecedorNotFound : SalvarFornecedorResult;
public record SalvarFornecedorForbidden : SalvarFornecedorResult;
public record SalvarFornecedorBadRequest(string Message) : SalvarFornecedorResult;

public abstract record ExcluirCadastroResult
{
    public static ExcluirCadastroResult Ok() => new ExcluirCadastroOk();
    public static ExcluirCadastroResult NotFound() => new ExcluirCadastroNotFound();
    public static ExcluirCadastroResult Forbidden() => new ExcluirCadastroForbidden();
    public static ExcluirCadastroResult Conflict(string message) => new ExcluirCadastroConflict(message);
}
public record ExcluirCadastroOk : ExcluirCadastroResult;
public record ExcluirCadastroNotFound : ExcluirCadastroResult;
public record ExcluirCadastroForbidden : ExcluirCadastroResult;
public record ExcluirCadastroConflict(string Message) : ExcluirCadastroResult;

internal static class CadastroVinculo
{
    public static async Task<string?> ValidarReciboAsync(
        ICompraRepository compras,
        IAdministradorSistemaRepository admins,
        Guid usuarioId,
        Guid? reciboId,
        CancellationToken cancellationToken)
    {
        if (reciboId is null || reciboId == Guid.Empty)
        {
            return null;
        }

        var compra = await compras.GetByIdAsync(reciboId.Value, cancellationToken);
        if (compra is null)
        {
            return "Recibo ou nota fiscal nao encontrado.";
        }

        var escopo = await ComprasEscopo.CriarAsync(admins, usuarioId, cancellationToken);
        if (!ComprasEscopo.PodeAcessar(escopo, compra.UsuarioId))
        {
            return "Sem permissao para vincular este recibo ou nota fiscal.";
        }

        return null;
    }
}

public sealed class ListarFornecedoresHandler : IRequestHandler<ListarFornecedoresQuery, ListarFornecedoresResult>
{
    private readonly IFornecedorRepository _fornecedores;
    private readonly IAdministradorSistemaRepository _admins;

    public ListarFornecedoresHandler(IFornecedorRepository fornecedores, IAdministradorSistemaRepository admins)
    {
        _fornecedores = fornecedores;
        _admins = admins;
    }

    public async Task<ListarFornecedoresResult> Handle(ListarFornecedoresQuery request, CancellationToken cancellationToken)
    {
        var admin = request.SomenteProprio
            ? false
            : await _admins.EhAdministradorAsync(request.UsuarioId, cancellationToken);
        var escopo = new EscopoDono(request.UsuarioId, admin);
        var lista = await _fornecedores.ListarAsync(escopo, request.Busca, request.ReciboId, cancellationToken);
        var quantidades = await _fornecedores.ContarRecibosAsync(lista.Select(x => x.Id).ToList(), cancellationToken);
        return new ListarFornecedoresResult(lista, quantidades);
    }
}

public sealed class ListarRecibosFornecedorHandler : IRequestHandler<ListarRecibosFornecedorQuery, ListarRecibosFornecedorResult>
{
    private readonly IFornecedorRepository _fornecedores;
    private readonly ICompraRepository _compras;
    private readonly IAdministradorSistemaRepository _admins;

    public ListarRecibosFornecedorHandler(
        IFornecedorRepository fornecedores,
        ICompraRepository compras,
        IAdministradorSistemaRepository admins)
    {
        _fornecedores = fornecedores;
        _compras = compras;
        _admins = admins;
    }

    public async Task<ListarRecibosFornecedorResult> Handle(ListarRecibosFornecedorQuery request, CancellationToken cancellationToken)
    {
        var escopo = await ComprasEscopo.CriarAsync(_admins, request.UsuarioId, cancellationToken);
        var fornecedor = await _fornecedores.GetByIdAsync(request.FornecedorId, cancellationToken);
        if (fornecedor is null)
        {
            return ListarRecibosFornecedorResult.NotFound();
        }

        if (!ComprasEscopo.PodeAcessar(escopo, fornecedor.UsuarioId))
        {
            return ListarRecibosFornecedorResult.Forbidden();
        }

        var recibos = await _compras.ListarPorFornecedorAsync(escopo, fornecedor.Id, cancellationToken);
        return ListarRecibosFornecedorResult.Ok(recibos);
    }
}

public sealed class CriarFornecedorHandler : IRequestHandler<CriarFornecedorCommand, SalvarFornecedorResult>
{
    private readonly IFornecedorRepository _fornecedores;
    private readonly IUnitOfWork _uow;

    public CriarFornecedorHandler(
        IFornecedorRepository fornecedores,
        IUnitOfWork uow)
    {
        _fornecedores = fornecedores;
        _uow = uow;
    }

    public async Task<SalvarFornecedorResult> Handle(CriarFornecedorCommand request, CancellationToken cancellationToken)
    {
        var nomeNorm = TextoNormalizado.Nome(request.Nome);
        var porNome = await _fornecedores.BuscarPorNomeNormalizadoAsync(request.UsuarioId, nomeNorm, cancellationToken);
        if (porNome is not null)
        {
            return SalvarFornecedorResult.BadRequest("Ja existe fornecedor ou prestador com este nome.");
        }

        var cnpj = TextoNormalizado.Digitos(request.CpfCnpj);
        if (!string.IsNullOrWhiteSpace(cnpj))
        {
            var existente = await _fornecedores.BuscarPorCpfCnpjAsync(request.UsuarioId, cnpj, cancellationToken);
            if (existente is not null)
            {
                return SalvarFornecedorResult.BadRequest("Ja existe fornecedor ou prestador com este CPF/CNPJ.");
            }
        }

        var fornecedor = Fornecedor.Criar(
            request.UsuarioId,
            request.Nome,
            TextoNormalizado.Nome(request.Nome),
            request.RazaoSocial,
            cnpj,
            request.Telefone,
            request.Endereco);
        await _fornecedores.AddAsync(fornecedor, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
        return SalvarFornecedorResult.Ok(fornecedor);
    }
}

public sealed class AtualizarFornecedorHandler : IRequestHandler<AtualizarFornecedorCommand, SalvarFornecedorResult>
{
    private readonly IFornecedorRepository _fornecedores;
    private readonly IAdministradorSistemaRepository _admins;
    private readonly IUnitOfWork _uow;

    public AtualizarFornecedorHandler(
        IFornecedorRepository fornecedores,
        IAdministradorSistemaRepository admins,
        IUnitOfWork uow)
    {
        _fornecedores = fornecedores;
        _admins = admins;
        _uow = uow;
    }

    public async Task<SalvarFornecedorResult> Handle(AtualizarFornecedorCommand request, CancellationToken cancellationToken)
    {
        var escopo = await ComprasEscopo.CriarAsync(_admins, request.UsuarioId, cancellationToken);
        var fornecedor = await _fornecedores.GetByIdAsync(request.Id, cancellationToken);
        if (fornecedor is null)
        {
            return SalvarFornecedorResult.NotFound();
        }

        if (!ComprasEscopo.PodeAcessar(escopo, fornecedor.UsuarioId))
        {
            return SalvarFornecedorResult.Forbidden();
        }

        var nomeNorm = TextoNormalizado.Nome(request.Nome);
        var outro = await _fornecedores.BuscarPorNomeNormalizadoAsync(fornecedor.UsuarioId, nomeNorm, cancellationToken);
        if (outro is not null && outro.Id != fornecedor.Id)
        {
            return SalvarFornecedorResult.BadRequest("Ja existe fornecedor ou prestador com este nome.");
        }

        var cnpj = TextoNormalizado.Digitos(request.CpfCnpj);
        if (!string.IsNullOrWhiteSpace(cnpj))
        {
            var porCnpj = await _fornecedores.BuscarPorCpfCnpjAsync(fornecedor.UsuarioId, cnpj, cancellationToken);
            if (porCnpj is not null && porCnpj.Id != fornecedor.Id)
            {
                return SalvarFornecedorResult.BadRequest("Ja existe fornecedor ou prestador com este CPF/CNPJ.");
            }
        }

        fornecedor.Atualizar(
            request.Nome,
            TextoNormalizado.Nome(request.Nome),
            request.RazaoSocial,
            cnpj,
            request.Telefone,
            request.Endereco);
        await _uow.SaveChangesAsync(cancellationToken);
        return SalvarFornecedorResult.Ok(fornecedor);
    }
}

public sealed class ExcluirFornecedorHandler : IRequestHandler<ExcluirFornecedorCommand, ExcluirCadastroResult>
{
    private readonly IFornecedorRepository _fornecedores;
    private readonly IAdministradorSistemaRepository _admins;
    private readonly IUnitOfWork _uow;

    public ExcluirFornecedorHandler(
        IFornecedorRepository fornecedores,
        IAdministradorSistemaRepository admins,
        IUnitOfWork uow)
    {
        _fornecedores = fornecedores;
        _admins = admins;
        _uow = uow;
    }

    public async Task<ExcluirCadastroResult> Handle(ExcluirFornecedorCommand request, CancellationToken cancellationToken)
    {
        var escopo = await ComprasEscopo.CriarAsync(_admins, request.UsuarioId, cancellationToken);
        var fornecedor = await _fornecedores.GetByIdAsync(request.Id, cancellationToken);
        if (fornecedor is null)
        {
            return ExcluirCadastroResult.NotFound();
        }

        if (!ComprasEscopo.PodeAcessar(escopo, fornecedor.UsuarioId))
        {
            return ExcluirCadastroResult.Forbidden();
        }

        var vinculados = await _fornecedores.ContarComprasVinculadasAsync(fornecedor.Id, cancellationToken);
        if (vinculados > 0)
        {
            return ExcluirCadastroResult.Conflict(
                "Nao e possivel excluir: ha compras vinculadas a este fornecedor ou prestador.");
        }

        _fornecedores.Remove(fornecedor);
        await _uow.SaveChangesAsync(cancellationToken);
        return ExcluirCadastroResult.Ok();
    }
}

public sealed class CriarFornecedorCommandValidator : AbstractValidator<CriarFornecedorCommand>
{
    public CriarFornecedorCommandValidator() => RuleFor(x => x.Nome).NotEmpty().MaximumLength(200);
}

public sealed class AtualizarFornecedorCommandValidator : AbstractValidator<AtualizarFornecedorCommand>
{
    public AtualizarFornecedorCommandValidator() => RuleFor(x => x.Nome).NotEmpty().MaximumLength(200);
}

public record ListarProdutosQuery(Guid UsuarioId, string? Busca, Guid? ReciboId) : IQuery<ListarProdutosResult>;
public record ListarProdutosResult(IReadOnlyList<Produto> Produtos);
public record CriarProdutoCommand(
    Guid UsuarioId,
    string Nome,
    string? Marca,
    string? Variante,
    string? UnidadeControle,
    decimal? ConteudoEmbalagem,
    string? Sinonimo,
    IReadOnlyList<string>? Sinonimos = null,
    string? CodigoExterno = null,
    string? NcmSh = null,
    string? Csosn = null,
    string? Cfop = null,
    decimal? ValorUnitario = null,
    decimal? ValorDesconto = null,
    decimal? ValorLiquido = null,
    decimal? BaseIcms = null,
    decimal? ValorIcms = null,
    decimal? ValorIpi = null,
    decimal? AliqIcms = null,
    decimal? AliqIpi = null,
    Guid? ReciboId = null) : ICommand<SalvarProdutoResult>;
public record AtualizarProdutoCommand(
    Guid UsuarioId,
    Guid Id,
    string Nome,
    string? Marca,
    string? Variante,
    string? UnidadeControle,
    decimal? ConteudoEmbalagem,
    string? Sinonimo,
    IReadOnlyList<string>? Sinonimos = null,
    string? CodigoExterno = null,
    string? NcmSh = null,
    string? Csosn = null,
    string? Cfop = null,
    decimal? ValorUnitario = null,
    decimal? ValorDesconto = null,
    decimal? ValorLiquido = null,
    decimal? BaseIcms = null,
    decimal? ValorIcms = null,
    decimal? ValorIpi = null,
    decimal? AliqIcms = null,
    decimal? AliqIpi = null,
    Guid? ReciboId = null) : ICommand<SalvarProdutoResult>;
public record ExcluirProdutoCommand(Guid UsuarioId, Guid Id) : ICommand<ExcluirCadastroResult>;

public abstract record SalvarProdutoResult
{
    public static SalvarProdutoResult Ok(Produto produto) => new SalvarProdutoOk(produto);
    public static SalvarProdutoResult NotFound() => new SalvarProdutoNotFound();
    public static SalvarProdutoResult Forbidden() => new SalvarProdutoForbidden();
    public static SalvarProdutoResult BadRequest(string message) => new SalvarProdutoBadRequest(message);
}
public record SalvarProdutoOk(Produto Produto) : SalvarProdutoResult;
public record SalvarProdutoNotFound : SalvarProdutoResult;
public record SalvarProdutoForbidden : SalvarProdutoResult;
public record SalvarProdutoBadRequest(string Message) : SalvarProdutoResult;

public sealed class ListarProdutosHandler : IRequestHandler<ListarProdutosQuery, ListarProdutosResult>
{
    private readonly IProdutoRepository _produtos;
    private readonly IAdministradorSistemaRepository _admins;

    public ListarProdutosHandler(IProdutoRepository produtos, IAdministradorSistemaRepository admins)
    {
        _produtos = produtos;
        _admins = admins;
    }

    public async Task<ListarProdutosResult> Handle(ListarProdutosQuery request, CancellationToken cancellationToken)
    {
        var escopo = await ComprasEscopo.CriarAsync(_admins, request.UsuarioId, cancellationToken);
        return new ListarProdutosResult(await _produtos.ListarAsync(escopo, request.Busca, request.ReciboId, cancellationToken));
    }
}

public sealed class CriarProdutoHandler : IRequestHandler<CriarProdutoCommand, SalvarProdutoResult>
{
    private readonly IProdutoRepository _produtos;
    private readonly ICompraRepository _compras;
    private readonly IAdministradorSistemaRepository _admins;
    private readonly IUnitOfWork _uow;

    public CriarProdutoHandler(
        IProdutoRepository produtos,
        ICompraRepository compras,
        IAdministradorSistemaRepository admins,
        IUnitOfWork uow)
    {
        _produtos = produtos;
        _compras = compras;
        _admins = admins;
        _uow = uow;
    }

    public async Task<SalvarProdutoResult> Handle(CriarProdutoCommand request, CancellationToken cancellationToken)
    {
        var vinculo = await CadastroVinculo.ValidarReciboAsync(
            _compras, _admins, request.UsuarioId, request.ReciboId, cancellationToken);
        if (vinculo is not null)
        {
            return SalvarProdutoResult.BadRequest(vinculo);
        }

        var produto = Produto.Criar(
            request.UsuarioId,
            request.Nome,
            TextoNormalizado.Nome(request.Nome),
            request.Marca,
            request.Variante,
            request.UnidadeControle ?? "un",
            request.ConteudoEmbalagem,
            request.ReciboId);
        CadastroSinonimos.Aplicar(produto, request.Sinonimo, request.Sinonimos);
        produto.AtualizarFiscal(
            request.CodigoExterno,
            request.NcmSh,
            request.Csosn,
            request.Cfop,
            request.ValorUnitario,
            request.ValorDesconto,
            request.ValorLiquido,
            request.BaseIcms,
            request.ValorIcms,
            request.ValorIpi,
            request.AliqIcms,
            request.AliqIpi);

        await _produtos.AddAsync(produto, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
        return SalvarProdutoResult.Ok(produto);
    }
}

public sealed class AtualizarProdutoHandler : IRequestHandler<AtualizarProdutoCommand, SalvarProdutoResult>
{
    private readonly IProdutoRepository _produtos;
    private readonly ICompraRepository _compras;
    private readonly IAdministradorSistemaRepository _admins;
    private readonly IUnitOfWork _uow;

    public AtualizarProdutoHandler(
        IProdutoRepository produtos,
        ICompraRepository compras,
        IAdministradorSistemaRepository admins,
        IUnitOfWork uow)
    {
        _produtos = produtos;
        _compras = compras;
        _admins = admins;
        _uow = uow;
    }

    public async Task<SalvarProdutoResult> Handle(AtualizarProdutoCommand request, CancellationToken cancellationToken)
    {
        var escopo = await ComprasEscopo.CriarAsync(_admins, request.UsuarioId, cancellationToken);
        var produto = await _produtos.GetByIdAsync(request.Id, cancellationToken);
        if (produto is null)
        {
            return SalvarProdutoResult.NotFound();
        }

        if (!ComprasEscopo.PodeAcessar(escopo, produto.UsuarioId))
        {
            return SalvarProdutoResult.Forbidden();
        }

        var vinculo = await CadastroVinculo.ValidarReciboAsync(
            _compras, _admins, request.UsuarioId, request.ReciboId, cancellationToken);
        if (vinculo is not null)
        {
            return SalvarProdutoResult.BadRequest(vinculo);
        }

        produto.Atualizar(
            request.Nome,
            TextoNormalizado.Nome(request.Nome),
            request.Marca,
            request.Variante,
            request.UnidadeControle ?? produto.UnidadeControle,
            request.ConteudoEmbalagem);
        CadastroSinonimos.Aplicar(produto, request.Sinonimo, request.Sinonimos);
        produto.AtualizarFiscal(
            request.CodigoExterno,
            request.NcmSh,
            request.Csosn,
            request.Cfop,
            request.ValorUnitario,
            request.ValorDesconto,
            request.ValorLiquido,
            request.BaseIcms,
            request.ValorIcms,
            request.ValorIpi,
            request.AliqIcms,
            request.AliqIpi);
        produto.DefinirRecibo(request.ReciboId);

        await _uow.SaveChangesAsync(cancellationToken);
        return SalvarProdutoResult.Ok(produto);
    }
}

public sealed class ExcluirProdutoHandler : IRequestHandler<ExcluirProdutoCommand, ExcluirCadastroResult>
{
    private readonly IProdutoRepository _produtos;
    private readonly IAdministradorSistemaRepository _admins;
    private readonly IUnitOfWork _uow;

    public ExcluirProdutoHandler(
        IProdutoRepository produtos,
        IAdministradorSistemaRepository admins,
        IUnitOfWork uow)
    {
        _produtos = produtos;
        _admins = admins;
        _uow = uow;
    }

    public async Task<ExcluirCadastroResult> Handle(ExcluirProdutoCommand request, CancellationToken cancellationToken)
    {
        var escopo = await ComprasEscopo.CriarAsync(_admins, request.UsuarioId, cancellationToken);
        var produto = await _produtos.GetByIdAsync(request.Id, cancellationToken);
        if (produto is null)
        {
            return ExcluirCadastroResult.NotFound();
        }

        if (!ComprasEscopo.PodeAcessar(escopo, produto.UsuarioId))
        {
            return ExcluirCadastroResult.Forbidden();
        }

        var vinculados = await _produtos.ContarItensVinculadosAsync(produto.Id, cancellationToken);
        if (vinculados > 0)
        {
            return ExcluirCadastroResult.Conflict("Nao e possivel excluir: ha itens de compra vinculados a este produto.");
        }

        _produtos.Remove(produto);
        await _uow.SaveChangesAsync(cancellationToken);
        return ExcluirCadastroResult.Ok();
    }
}

internal static class CadastroSinonimos
{
    public static void Aplicar(Produto produto, string? sinonimo, IReadOnlyList<string>? sinonimos)
    {
        if (sinonimos is not null)
        {
            produto.SubstituirSinonimos(
                sinonimos
                    .Select(s => (s, TextoNormalizado.Nome(s)))
                    .ToList());
            return;
        }

        if (!string.IsNullOrWhiteSpace(sinonimo))
        {
            produto.AdicionarSinonimo(sinonimo, TextoNormalizado.Nome(sinonimo));
        }
    }
}

public sealed class CriarProdutoCommandValidator : AbstractValidator<CriarProdutoCommand>
{
    public CriarProdutoCommandValidator() => RuleFor(x => x.Nome).NotEmpty().MaximumLength(200);
}

public sealed class AtualizarProdutoCommandValidator : AbstractValidator<AtualizarProdutoCommand>
{
    public AtualizarProdutoCommandValidator() => RuleFor(x => x.Nome).NotEmpty().MaximumLength(200);
}

public record ListarServicosQuery(Guid UsuarioId, string? Busca, Guid? ReciboId) : IQuery<ListarServicosResult>;
public record ListarServicosResult(IReadOnlyList<Servico> Servicos);
public record CriarServicoCommand(
    Guid UsuarioId,
    string Nome,
    string? UnidadeControle,
    string? CodigoExterno,
    string? NcmSh,
    string? Csosn,
    string? Cfop,
    decimal? ValorUnitario,
    decimal? ValorDesconto,
    decimal? ValorLiquido,
    decimal? BaseIcms,
    decimal? ValorIcms,
    decimal? ValorIpi,
    decimal? AliqIcms,
    decimal? AliqIpi,
    Guid? ReciboId = null) : ICommand<SalvarServicoResult>;
public record AtualizarServicoCommand(
    Guid UsuarioId,
    Guid Id,
    string Nome,
    string? UnidadeControle,
    string? CodigoExterno,
    string? NcmSh,
    string? Csosn,
    string? Cfop,
    decimal? ValorUnitario,
    decimal? ValorDesconto,
    decimal? ValorLiquido,
    decimal? BaseIcms,
    decimal? ValorIcms,
    decimal? ValorIpi,
    decimal? AliqIcms,
    decimal? AliqIpi,
    Guid? ReciboId = null) : ICommand<SalvarServicoResult>;
public record ExcluirServicoCommand(Guid UsuarioId, Guid Id) : ICommand<ExcluirCadastroResult>;

public abstract record SalvarServicoResult
{
    public static SalvarServicoResult Ok(Servico servico) => new SalvarServicoOk(servico);
    public static SalvarServicoResult NotFound() => new SalvarServicoNotFound();
    public static SalvarServicoResult Forbidden() => new SalvarServicoForbidden();
    public static SalvarServicoResult BadRequest(string message) => new SalvarServicoBadRequest(message);
}
public record SalvarServicoOk(Servico Servico) : SalvarServicoResult;
public record SalvarServicoNotFound : SalvarServicoResult;
public record SalvarServicoForbidden : SalvarServicoResult;
public record SalvarServicoBadRequest(string Message) : SalvarServicoResult;

public sealed class ListarServicosHandler : IRequestHandler<ListarServicosQuery, ListarServicosResult>
{
    private readonly IServicoRepository _servicos;
    private readonly IAdministradorSistemaRepository _admins;

    public ListarServicosHandler(IServicoRepository servicos, IAdministradorSistemaRepository admins)
    {
        _servicos = servicos;
        _admins = admins;
    }

    public async Task<ListarServicosResult> Handle(ListarServicosQuery request, CancellationToken cancellationToken)
    {
        var escopo = await ComprasEscopo.CriarAsync(_admins, request.UsuarioId, cancellationToken);
        return new ListarServicosResult(await _servicos.ListarAsync(escopo, request.Busca, request.ReciboId, cancellationToken));
    }
}

public sealed class CriarServicoHandler : IRequestHandler<CriarServicoCommand, SalvarServicoResult>
{
    private readonly IServicoRepository _servicos;
    private readonly ICompraRepository _compras;
    private readonly IAdministradorSistemaRepository _admins;
    private readonly IUnitOfWork _uow;

    public CriarServicoHandler(
        IServicoRepository servicos,
        ICompraRepository compras,
        IAdministradorSistemaRepository admins,
        IUnitOfWork uow)
    {
        _servicos = servicos;
        _compras = compras;
        _admins = admins;
        _uow = uow;
    }

    public async Task<SalvarServicoResult> Handle(CriarServicoCommand request, CancellationToken cancellationToken)
    {
        var vinculo = await CadastroVinculo.ValidarReciboAsync(
            _compras, _admins, request.UsuarioId, request.ReciboId, cancellationToken);
        if (vinculo is not null)
        {
            return SalvarServicoResult.BadRequest(vinculo);
        }

        var servico = Servico.Criar(
            request.UsuarioId,
            request.Nome,
            TextoNormalizado.Nome(request.Nome),
            request.UnidadeControle,
            request.ReciboId);
        servico.AtualizarFiscal(
            request.CodigoExterno,
            request.NcmSh,
            request.Csosn,
            request.Cfop,
            request.ValorUnitario,
            request.ValorDesconto,
            request.ValorLiquido,
            request.BaseIcms,
            request.ValorIcms,
            request.ValorIpi,
            request.AliqIcms,
            request.AliqIpi);
        await _servicos.AddAsync(servico, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
        return SalvarServicoResult.Ok(servico);
    }
}

public sealed class AtualizarServicoHandler : IRequestHandler<AtualizarServicoCommand, SalvarServicoResult>
{
    private readonly IServicoRepository _servicos;
    private readonly ICompraRepository _compras;
    private readonly IAdministradorSistemaRepository _admins;
    private readonly IUnitOfWork _uow;

    public AtualizarServicoHandler(
        IServicoRepository servicos,
        ICompraRepository compras,
        IAdministradorSistemaRepository admins,
        IUnitOfWork uow)
    {
        _servicos = servicos;
        _compras = compras;
        _admins = admins;
        _uow = uow;
    }

    public async Task<SalvarServicoResult> Handle(AtualizarServicoCommand request, CancellationToken cancellationToken)
    {
        var escopo = await ComprasEscopo.CriarAsync(_admins, request.UsuarioId, cancellationToken);
        var servico = await _servicos.GetByIdAsync(request.Id, cancellationToken);
        if (servico is null)
        {
            return SalvarServicoResult.NotFound();
        }

        if (!ComprasEscopo.PodeAcessar(escopo, servico.UsuarioId))
        {
            return SalvarServicoResult.Forbidden();
        }

        var vinculo = await CadastroVinculo.ValidarReciboAsync(
            _compras, _admins, request.UsuarioId, request.ReciboId, cancellationToken);
        if (vinculo is not null)
        {
            return SalvarServicoResult.BadRequest(vinculo);
        }

        servico.Atualizar(request.Nome, TextoNormalizado.Nome(request.Nome), request.UnidadeControle);
        servico.AtualizarFiscal(
            request.CodigoExterno,
            request.NcmSh,
            request.Csosn,
            request.Cfop,
            request.ValorUnitario,
            request.ValorDesconto,
            request.ValorLiquido,
            request.BaseIcms,
            request.ValorIcms,
            request.ValorIpi,
            request.AliqIcms,
            request.AliqIpi);
        servico.DefinirRecibo(request.ReciboId);
        await _uow.SaveChangesAsync(cancellationToken);
        return SalvarServicoResult.Ok(servico);
    }
}

public sealed class ExcluirServicoHandler : IRequestHandler<ExcluirServicoCommand, ExcluirCadastroResult>
{
    private readonly IServicoRepository _servicos;
    private readonly IAdministradorSistemaRepository _admins;
    private readonly IUnitOfWork _uow;

    public ExcluirServicoHandler(
        IServicoRepository servicos,
        IAdministradorSistemaRepository admins,
        IUnitOfWork uow)
    {
        _servicos = servicos;
        _admins = admins;
        _uow = uow;
    }

    public async Task<ExcluirCadastroResult> Handle(ExcluirServicoCommand request, CancellationToken cancellationToken)
    {
        var escopo = await ComprasEscopo.CriarAsync(_admins, request.UsuarioId, cancellationToken);
        var servico = await _servicos.GetByIdAsync(request.Id, cancellationToken);
        if (servico is null)
        {
            return ExcluirCadastroResult.NotFound();
        }

        if (!ComprasEscopo.PodeAcessar(escopo, servico.UsuarioId))
        {
            return ExcluirCadastroResult.Forbidden();
        }

        var vinculados = await _servicos.ContarItensVinculadosAsync(servico.Id, cancellationToken);
        if (vinculados > 0)
        {
            return ExcluirCadastroResult.Conflict("Nao e possivel excluir: ha itens de compra vinculados a este servico.");
        }

        _servicos.Remove(servico);
        await _uow.SaveChangesAsync(cancellationToken);
        return ExcluirCadastroResult.Ok();
    }
}

public sealed class CriarServicoCommandValidator : AbstractValidator<CriarServicoCommand>
{
    public CriarServicoCommandValidator() => RuleFor(x => x.Nome).NotEmpty().MaximumLength(200);
}

public sealed class AtualizarServicoCommandValidator : AbstractValidator<AtualizarServicoCommand>
{
    public AtualizarServicoCommandValidator() => RuleFor(x => x.Nome).NotEmpty().MaximumLength(200);
}
