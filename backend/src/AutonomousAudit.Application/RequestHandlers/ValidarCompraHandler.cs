using AutonomousAudit.Application;
using AutonomousAudit.Application.Data;
using AutonomousAudit.Domain;
using FluentValidation;
using MediatR;

namespace AutonomousAudit.Application.RequestHandlers;

public sealed class ValidarCompraCommandValidator : AbstractValidator<ValidarCompraCommand>
{
    public ValidarCompraCommandValidator()
    {
        RuleFor(x => x.Fornecedor.Nome).NotEmpty();
        RuleFor(x => x.Itens).NotEmpty();
        RuleForEach(x => x.Itens).ChildRules(item =>
        {
            item.RuleFor(i => i.Descricao).NotEmpty();
            item.RuleFor(i => i.Quantidade).GreaterThan(0);
        });
    }
}

public sealed class ValidarCompraHandler : IRequestHandler<ValidarCompraCommand, ValidarCompraResult>
{
    private readonly ICompraRepository _compras;
    private readonly IFornecedorRepository _fornecedores;
    private readonly IProdutoRepository _produtos;
    private readonly IAdministradorSistemaRepository _admins;
    private readonly IDropboxDocumentos _dropbox;
    private readonly ICompraDropboxPaths _paths;
    private readonly IUnitOfWork _uow;

    public ValidarCompraHandler(
        ICompraRepository compras,
        IFornecedorRepository fornecedores,
        IProdutoRepository produtos,
        IAdministradorSistemaRepository admins,
        IDropboxDocumentos dropbox,
        ICompraDropboxPaths paths,
        IUnitOfWork uow)
    {
        _compras = compras;
        _fornecedores = fornecedores;
        _produtos = produtos;
        _admins = admins;
        _dropbox = dropbox;
        _paths = paths;
        _uow = uow;
    }

    public async Task<ValidarCompraResult> Handle(ValidarCompraCommand request, CancellationToken cancellationToken)
    {
        var escopo = await ComprasEscopo.CriarAsync(_admins, request.UsuarioId, cancellationToken);
        var compra = await _compras.GetByIdDetalheAsync(request.CompraId, cancellationToken);
        if (compra is null)
        {
            return ValidarCompraResult.NotFound();
        }

        if (!ComprasEscopo.PodeAcessar(escopo, compra.UsuarioId))
        {
            return ValidarCompraResult.Forbidden();
        }

        if (compra.Status is not CompraStatus.Revisao and not CompraStatus.Validada)
        {
            return ValidarCompraResult.BadRequest("Valide a compra depois da extracao (status revisao).");
        }

        var dono = compra.UsuarioId;
        var fornecedor = await ResolverFornecedorAsync(dono, request.Fornecedor, cancellationToken);
        var idsMantidos = new HashSet<Guid>();
        foreach (var body in request.Itens)
        {
            var produtoId = await ResolverProdutoAsync(dono, body, cancellationToken);
            CompraItem item;
            if (body.Id is Guid idExistente)
            {
                item = compra.Itens.FirstOrDefault(x => x.Id == idExistente)
                    ?? CompraItem.Criar(
                        compra.Id,
                        compra.Itens.Count + 1,
                        body.Descricao,
                        body.Codigo,
                        body.Quantidade,
                        body.Unidade,
                        body.PrecoUnitario,
                        body.Desconto,
                        body.Total);
                if (!compra.Itens.Any(x => x.Id == item.Id))
                {
                    compra.IncluirItem(item);
                }
            }
            else
            {
                item = CompraItem.Criar(
                    compra.Id,
                    compra.Itens.Count + 1,
                    body.Descricao,
                    body.Codigo,
                    body.Quantidade,
                    body.Unidade,
                    body.PrecoUnitario,
                    body.Desconto,
                    body.Total);
                compra.IncluirItem(item);
            }

            item.AtualizarLancamento(
                body.Descricao,
                body.Codigo,
                body.Quantidade,
                body.Unidade,
                body.PrecoUnitario,
                body.Desconto,
                body.Total);
            item.VincularProduto(produtoId);
            idsMantidos.Add(item.Id);
        }

        foreach (var antigo in compra.Itens.Where(x => !idsMantidos.Contains(x.Id)).ToList())
        {
            compra.RemoverItem(antigo.Id);
        }

        var dataCompra = request.DataCompra?.ToUniversalTime() ?? compra.DataCompra ?? DateTimeOffset.UtcNow;
        var chave = ComprasEscopo.ChaveDuplicidade(
            fornecedor.CpfCnpj,
            fornecedor.Nome,
            request.NumeroRecibo,
            dataCompra,
            request.Total);
        compra.AplicarValidacao(
            fornecedor.Id,
            dataCompra,
            request.NumeroRecibo,
            request.Subtotal,
            request.Descontos,
            request.Acrescimos,
            request.Total,
            request.FormaPagamento,
            string.IsNullOrWhiteSpace(chave) ? null : chave);

        var documento = compra.Documentos.OrderByDescending(x => x.DataAtualizacao).FirstOrDefault();
        if (documento is not null)
        {
            var destino = _paths.Arquivo(compra.UsuarioId, compra.Id, documento.Id);
            if (!string.Equals(documento.DropboxPath, destino, StringComparison.OrdinalIgnoreCase))
            {
                var movido = await _dropbox.MoverAsync(documento.DropboxPath, destino, cancellationToken);
                documento.MoverPara(movido.Caminho);
                if (!string.IsNullOrWhiteSpace(movido.Id))
                {
                    documento.RegistrarDropbox(movido.Caminho, movido.Id);
                }
            }
        }

        await _uow.SaveChangesAsync(cancellationToken);
        return ValidarCompraResult.Ok(compra);
    }

    private async Task<Fornecedor> ResolverFornecedorAsync(
        Guid usuarioId,
        ValidarFornecedorBody body,
        CancellationToken cancellationToken)
    {
        if (body.Id is Guid id)
        {
            var existente = await _fornecedores.GetByIdAsync(id, cancellationToken);
            if (existente is not null && existente.UsuarioId == usuarioId)
            {
                existente.Atualizar(
                    body.Nome,
                    TextoNormalizado.Nome(body.Nome),
                    body.RazaoSocial,
                    TextoNormalizado.Digitos(body.CpfCnpj),
                    body.Telefone,
                    body.Endereco);
                return existente;
            }
        }

        var cnpj = TextoNormalizado.Digitos(body.CpfCnpj);
        return await FornecedorGarantia.ResolverAsync(
            _fornecedores,
            usuarioId,
            body.Nome,
            body.RazaoSocial,
            cnpj,
            body.Telefone,
            body.Endereco,
            cancellationToken);
    }

    private async Task<Guid?> ResolverProdutoAsync(
        Guid usuarioId,
        ValidarItemBody body,
        CancellationToken cancellationToken)
    {
        if (body.ProdutoId is Guid id)
        {
            var existente = await _produtos.GetByIdAsync(id, cancellationToken);
            if (existente is not null && existente.UsuarioId == usuarioId)
            {
                existente.AdicionarSinonimo(body.Descricao, TextoNormalizado.Nome(body.Descricao));
                return existente.Id;
            }
        }

        if (body.ProdutoNovo is { } novoBody && !string.IsNullOrWhiteSpace(novoBody.Nome))
        {
            var produto = Produto.Criar(
                usuarioId,
                novoBody.Nome,
                TextoNormalizado.Nome(novoBody.Nome),
                novoBody.Marca,
                novoBody.Variante,
                novoBody.UnidadeControle ?? "un",
                novoBody.ConteudoEmbalagem);
            produto.AdicionarSinonimo(body.Descricao, TextoNormalizado.Nome(body.Descricao));
            await _produtos.AddAsync(produto, cancellationToken);
            return produto.Id;
        }

        var porNome = await _produtos.BuscarPorNomeOuSinonimoAsync(
            usuarioId,
            TextoNormalizado.Nome(body.Descricao),
            cancellationToken);
        porNome?.AdicionarSinonimo(body.Descricao, TextoNormalizado.Nome(body.Descricao));
        return porNome?.Id;
    }
}
