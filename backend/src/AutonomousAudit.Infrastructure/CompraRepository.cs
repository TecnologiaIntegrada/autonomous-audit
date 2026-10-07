using AutonomousAudit.Application;
using AutonomousAudit.Application.Data;
using AutonomousAudit.Application.RequestHandlers;
using AutonomousAudit.Domain;
using Microsoft.EntityFrameworkCore;

namespace AutonomousAudit.Infrastructure;

public sealed class CompraRepository : ICompraRepository
{
    private readonly AuditDbContext _db;

    public CompraRepository(AuditDbContext db)
    {
        _db = db;
    }

    public async Task AddAsync(Compra compra, CancellationToken cancellationToken = default) =>
        await _db.Compras.AddAsync(compra, cancellationToken);

    public Task<Compra?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.Compras
            .Include(x => x.Anexos)
            .Include(x => x.Capturas)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<Compra?> GetByIdDetalheAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.Compras
            .Include(x => x.Fornecedor)
            .Include(x => x.Anexos)
            .Include(x => x.Documentos)
            .Include(x => x.Capturas)
            .Include(x => x.Itens)
            .ThenInclude(i => i.Produto)
            .Include(x => x.Itens)
            .ThenInclude(i => i.Servico)
            .Include(x => x.NfeDestinatario)
            .Include(x => x.NfeImposto)
            .Include(x => x.NfeTransportador)
            .Include(x => x.NfeAdicionais)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Compra>> ListarAsync(
        EscopoDono escopo,
        string? status,
        bool somenteOrigem = false,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Compras
            .AsNoTracking()
            .Include(x => x.Fornecedor)
            .Include(x => x.Anexos)
            .Include(x => x.Documentos)
            .AsQueryable();

        if (!escopo.EhAdministrador)
        {
            query = query.Where(x => x.UsuarioId == escopo.UsuarioId);
        }

        query = query.Where(x => x.Status != CompraStatus.Rascunho || x.Anexos.Any());
        if (somenteOrigem)
        {
            query = query.Where(x => x.ReciboOrigemId == null);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(x => x.Status == status);
        }

        return await query.OrderByDescending(x => x.DataEnvio).Take(500).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Compra>> ListarPorFornecedorAsync(
        EscopoDono escopo,
        Guid fornecedorId,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Compras
            .AsNoTracking()
            .Include(x => x.Fornecedor)
            .Include(x => x.Anexos)
            .Include(x => x.Documentos)
            .Where(x => x.FornecedorId == fornecedorId);

        if (!escopo.EhAdministrador)
        {
            query = query.Where(x => x.UsuarioId == escopo.UsuarioId);
        }

        return await query.OrderBy(x => x.DataEnvio).ThenBy(x => x.Id).ToListAsync(cancellationToken);
    }

    public Task<CapturaSessao?> GetCapturaPorTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        _db.CapturaSessoes.FirstOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);

    public Task<CapturaSessao?> GetCapturaPorSessionHashAsync(string sessionHash, CancellationToken cancellationToken = default) =>
        _db.CapturaSessoes.FirstOrDefaultAsync(x => x.SessionTokenHash == sessionHash, cancellationToken);

    public async Task<IReadOnlyList<Compra>> BuscarDuplicadasAsync(
        Guid usuarioId,
        Guid compraId,
        string? hashArquivo,
        string? chaveDuplicidade,
        CancellationToken cancellationToken = default)
    {
        var origemId = await _db.Compras.AsNoTracking()
            .Where(x => x.Id == compraId)
            .Select(x => x.ReciboOrigemId)
            .FirstOrDefaultAsync(cancellationToken);
        var loteId = origemId ?? compraId;

        var query = _db.Compras.AsNoTracking().Where(x =>
            x.UsuarioId == usuarioId
            && x.Id != compraId
            && x.Id != loteId
            && (x.ReciboOrigemId == null || x.ReciboOrigemId != loteId));
        if (string.IsNullOrWhiteSpace(hashArquivo) && string.IsNullOrWhiteSpace(chaveDuplicidade))
        {
            return [];
        }

        query = query.Where(x =>
            (!string.IsNullOrWhiteSpace(hashArquivo) && x.HashArquivo == hashArquivo) ||
            (!string.IsNullOrWhiteSpace(chaveDuplicidade) && x.ChaveDuplicidade == chaveDuplicidade));

        return await query.OrderByDescending(x => x.DataEnvio).Take(10).ToListAsync(cancellationToken);
    }

    public async Task<ComprasResumo> ResumoAsync(
        EscopoDono escopo,
        DateTimeOffset inicio,
        DateTimeOffset fim,
        Guid? fornecedorId,
        CancellationToken cancellationToken = default)
    {
        var compras = _db.Compras.AsNoTracking().Where(x => x.DataEnvio >= inicio && x.DataEnvio <= fim);
        if (!escopo.EhAdministrador)
        {
            compras = compras.Where(x => x.UsuarioId == escopo.UsuarioId);
        }

        if (fornecedorId.HasValue)
        {
            compras = compras.Where(x => x.FornecedorId == fornecedorId);
        }

        compras = compras.Where(x => x.Status != CompraStatus.Rascunho);

        var lista = await compras.Include(x => x.Fornecedor).ToListAsync(cancellationToken);
        var porFornecedor = lista
            .GroupBy(x => new { x.FornecedorId, Nome = x.Fornecedor?.Nome ?? "Sem fornecedor" })
            .Select(g => new ComprasResumoFornecedor(
                g.Key.FornecedorId,
                g.Key.Nome,
                g.Count(),
                g.Sum(x => x.Total ?? 0)))
            .OrderByDescending(x => x.Total)
            .Take(8)
            .ToList();

        return new ComprasResumo(
            lista.Count,
            lista.Count(x => x.Status == CompraStatus.Rascunho),
            lista.Count(x => x.Status == CompraStatus.Processando),
            lista.Count(x => x.Status == CompraStatus.Revisao),
            lista.Count(x => x.Status == CompraStatus.Validada),
            lista.Count(x => x.Status == CompraStatus.Concluida),
            lista.Count(x => x.Status == CompraStatus.FalhaProcessamento),
            lista.Where(x => x.Status is CompraStatus.Processado or CompraStatus.Validada or CompraStatus.Concluida).Sum(x => x.Total ?? 0),
            porFornecedor,
            lista.Count(x => x.Status == CompraStatus.Processado));
    }

    public Task RemoveAsync(Compra compra, CancellationToken cancellationToken = default)
    {
        _db.Compras.Remove(compra);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<Guid>> ListarIdsRascunhosAnterioresAoDiaAsync(
        Guid usuarioId,
        DateTimeOffset inicioDoDia,
        CancellationToken cancellationToken = default) =>
        await _db.Compras.AsNoTracking()
            .Where(x =>
                x.UsuarioId == usuarioId
                && x.Status == CompraStatus.Rascunho
                && x.ReciboOrigemId == null
                && x.DataEnvio < inicioDoDia)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Compra>> ListarDerivadasAsync(
        Guid reciboOrigemId,
        CancellationToken cancellationToken = default) =>
        await _db.Compras
            .Include(x => x.Itens)
            .Include(x => x.Anexos)
            .Include(x => x.Documentos)
            .Where(x => x.ReciboOrigemId == reciboOrigemId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<RelatorioItemLinha>> RelatorioItensAsync(
        EscopoDono escopo,
        DateTimeOffset inicio,
        DateTimeOffset fim,
        Guid? fornecedorId,
        int limite,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Compras.AsNoTracking()
            .Include(x => x.Fornecedor)
            .Include(x => x.Itens)
            .ThenInclude(i => i.Produto)
            .Include(x => x.Itens)
            .ThenInclude(i => i.Servico)
            .Include(x => x.NfeDestinatario)
            .Include(x => x.NfeImposto)
            .Include(x => x.NfeTransportador)
            .Include(x => x.NfeAdicionais)
            .Where(x => x.UsuarioId == escopo.UsuarioId)
            .Where(x => x.Status != CompraStatus.Rascunho)
            .Where(x =>
                (x.DataEnvio >= inicio && x.DataEnvio <= fim)
                || (x.DataCompra != null && x.DataCompra >= inicio && x.DataCompra <= fim));

        if (fornecedorId.HasValue)
        {
            query = query.Where(x => x.FornecedorId == fornecedorId);
        }

        var limiteSeguro = Math.Clamp(limite, 1, RelatorioItensHandler.LimiteMaximo);
        var compras = await query.ToListAsync(cancellationToken);
        var compraIds = compras.Select(x => x.Id).ToList();
        var reciboIds = compras.Select(x => x.ReciboOrigemId ?? x.Id).Distinct().ToList();
        var docsBrutos = await _db.CompraDocumentos.AsNoTracking()
            .Where(d => reciboIds.Contains(d.CompraId) || compraIds.Contains(d.CompraId))
            .Select(d => new { d.CompraId, d.Id, d.DataAtualizacao })
            .ToListAsync(cancellationToken);
        var docs = docsBrutos
            .GroupBy(d => d.CompraId)
            .ToDictionary(
                g => g.Key,
                g => (Guid?)g.OrderByDescending(x => x.DataAtualizacao).Select(x => x.Id).First());

        var linhas = new List<RelatorioItemLinha>();
        foreach (var compra in compras
            .OrderBy(x => x.Fornecedor?.Nome)
            .ThenByDescending(x => x.DataEnvio))
        {
            var itens = compra.Itens.OrderBy(x => x.Ordem).ToList();
            if (itens.Count == 0)
            {
                linhas.Add(MapearRelatorio(compra, null, docs));
            }
            else
            {
                foreach (var item in itens)
                {
                    linhas.Add(MapearRelatorio(compra, item, docs));
                }
            }

            if (linhas.Count >= limiteSeguro)
            {
                break;
            }
        }

        return linhas.Take(limiteSeguro).ToList();
    }

    private static RelatorioItemLinha MapearRelatorio(
        Compra compra,
        CompraItem? item,
        IReadOnlyDictionary<Guid, Guid?> docs)
    {
        var reciboId = compra.ReciboOrigemId ?? compra.Id;
        var arquivoId = docs.TryGetValue(reciboId, out var arquivoLote)
            ? arquivoLote
            : docs.TryGetValue(compra.Id, out var arquivoCompra) ? arquivoCompra : null;
        var prod = item?.Produto;
        var serv = item?.Servico;
        var dest = compra.NfeDestinatario;
        var imp = compra.NfeImposto;
        var transp = compra.NfeTransportador;
        var adic = compra.NfeAdicionais;

        return new RelatorioItemLinha
        {
            UsuarioId = compra.UsuarioId,
            ReciboId = reciboId,
            CompraId = compra.Id,
            ReciboOrigemId = compra.ReciboOrigemId,
            ArquivoId = arquivoId,
            Data = compra.DataCompra ?? compra.DataEnvio,
            Status = compra.Status,
            NumeroRecibo = compra.NumeroRecibo,
            Subtotal = compra.Subtotal,
            Descontos = compra.Descontos,
            Acrescimos = compra.Acrescimos,
            TotalCompra = compra.Total,
            FormaPagamento = compra.FormaPagamento,
            TipoDocumento = compra.TipoDocumento,
            TipoItemCompra = compra.TipoItem,
            DanfeTipo = compra.DanfeTipo,
            Serie = compra.Serie,
            Folha = compra.Folha,
            ChaveAcesso = compra.ChaveAcesso,
            CodigoBarras = compra.CodigoBarras,
            ProtocoloAutorizacao = compra.ProtocoloAutorizacao,
            ProtocoloData = compra.ProtocoloData,
            NaturezaOperacao = compra.NaturezaOperacao,
            InscricaoEstadual = compra.InscricaoEstadual,
            InscricaoEstadualSt = compra.InscricaoEstadualSt,
            DataEmissao = compra.DataEmissao,
            FornecedorId = compra.FornecedorId,
            FornecedorNome = compra.Fornecedor?.Nome,
            FornecedorRazaoSocial = compra.Fornecedor?.RazaoSocial,
            FornecedorCpfCnpj = compra.Fornecedor?.CpfCnpj,
            FornecedorTelefone = compra.Fornecedor?.Telefone,
            FornecedorEndereco = compra.Fornecedor?.Endereco,
            ProdutoId = item?.ProdutoId,
            ProdutoNome = prod?.Nome,
            ProdutoMarca = prod?.Marca,
            ProdutoVariante = prod?.Variante,
            ConteudoEmbalagem = prod?.ConteudoEmbalagem,
            ProdutoCodigoExterno = prod?.CodigoExterno,
            ProdutoNcmSh = prod?.NcmSh,
            ProdutoCsosn = prod?.Csosn,
            ProdutoCfop = prod?.Cfop,
            ProdutoValorUnitario = prod?.ValorUnitario,
            ProdutoValorDesconto = prod?.ValorDesconto,
            ProdutoValorLiquido = prod?.ValorLiquido,
            ProdutoBaseIcms = prod?.BaseIcms,
            ProdutoValorIcms = prod?.ValorIcms,
            ProdutoValorIpi = prod?.ValorIpi,
            ProdutoAliqIcms = prod?.AliqIcms,
            ProdutoAliqIpi = prod?.AliqIpi,
            ServicoId = item?.ServicoId,
            ServicoNome = serv?.Nome,
            ServicoCodigoExterno = serv?.CodigoExterno,
            ServicoUnidadeControle = serv?.UnidadeControle,
            ServicoNcmSh = serv?.NcmSh,
            ServicoCsosn = serv?.Csosn,
            ServicoCfop = serv?.Cfop,
            ServicoValorUnitario = serv?.ValorUnitario,
            ServicoValorDesconto = serv?.ValorDesconto,
            ServicoValorLiquido = serv?.ValorLiquido,
            ServicoBaseIcms = serv?.BaseIcms,
            ServicoValorIcms = serv?.ValorIcms,
            ServicoValorIpi = serv?.ValorIpi,
            ServicoAliqIcms = serv?.AliqIcms,
            ServicoAliqIpi = serv?.AliqIpi,
            Codigo = item?.CodigoImpresso,
            Descricao = item?.DescricaoOriginal ?? string.Empty,
            Quantidade = item?.Quantidade ?? 0,
            Unidade = item?.Unidade,
            PrecoUnitario = item?.PrecoUnitario,
            Desconto = item?.Desconto,
            Total = item?.Total,
            TipoItem = item?.TipoItem ?? compra.TipoItem,
            ItemNcmSh = item?.NcmSh,
            ItemCsosn = item?.Csosn,
            ItemCfop = item?.Cfop,
            ItemValorLiquido = item?.ValorLiquido,
            ItemBaseIcms = item?.BaseIcms,
            ItemValorIcms = item?.ValorIcms,
            ItemValorIpi = item?.ValorIpi,
            ItemAliqIcms = item?.AliqIcms,
            ItemAliqIpi = item?.AliqIpi,
            DestinatarioNome = dest?.NomeRazaoSocial,
            DestinatarioCpfCnpj = dest?.CpfCnpj,
            DestinatarioEndereco = dest?.Endereco,
            DestinatarioBairro = dest?.Bairro,
            DestinatarioCep = dest?.Cep,
            DestinatarioMunicipio = dest?.Municipio,
            DestinatarioUf = dest?.Uf,
            DestinatarioTelefone = dest?.Telefone,
            DestinatarioIe = dest?.InscricaoEstadual,
            DestinatarioDataEmissao = dest?.DataEmissao,
            DestinatarioDataSaida = dest?.DataSaida,
            DestinatarioHoraSaida = dest?.HoraSaida,
            ImpostoBaseIcms = imp?.BaseIcms,
            ImpostoValorIcms = imp?.ValorIcms,
            ImpostoBaseIcmsSt = imp?.BaseIcmsSt,
            ImpostoValorIcmsSt = imp?.ValorIcmsSt,
            ImpostoValorTotalProdutos = imp?.ValorTotalProdutos,
            ImpostoValorFrete = imp?.ValorFrete,
            ImpostoValorSeguro = imp?.ValorSeguro,
            ImpostoDesconto = imp?.Desconto,
            ImpostoOutrasDespesas = imp?.OutrasDespesas,
            ImpostoValorIpi = imp?.ValorIpi,
            ImpostoValorTotalNota = imp?.ValorTotalNota,
            TransportadorNome = transp?.NomeRazaoSocial,
            TransportadorFretePorConta = transp?.FretePorConta,
            TransportadorCodigoAntt = transp?.CodigoAntt,
            TransportadorPlaca = transp?.Placa,
            TransportadorUf = transp?.Uf,
            TransportadorCpfCnpj = transp?.CpfCnpj,
            TransportadorEndereco = transp?.Endereco,
            TransportadorMunicipio = transp?.Municipio,
            TransportadorUfEndereco = transp?.UfEndereco,
            TransportadorIe = transp?.InscricaoEstadual,
            TransportadorQuantidadeVolumes = transp?.QuantidadeVolumes,
            TransportadorEspecie = transp?.Especie,
            TransportadorMarca = transp?.Marca,
            TransportadorNumeracao = transp?.Numeracao,
            TransportadorPesoBruto = transp?.PesoBruto,
            TransportadorPesoLiquido = transp?.PesoLiquido,
            AdicionaisInformacoes = adic?.InformacoesComplementares,
            AdicionaisReservadoFisco = adic?.ReservadoAoFisco,
            AdicionaisDataHoraImpressao = adic?.DataHoraImpressao
        };
    }

    public async Task<long> SomarTamanhoAnexosDoUsuarioAsync(
        Guid usuarioId,
        CancellationToken cancellationToken = default)
    {
        var total = await (
            from anexo in _db.CompraAnexos.AsNoTracking()
            join compra in _db.Compras.AsNoTracking() on anexo.CompraId equals compra.Id
            where compra.UsuarioId == usuarioId
            select (long?)anexo.TamanhoBytes).SumAsync(cancellationToken);
        return total ?? 0;
    }
}

public sealed class FornecedorRepository : IFornecedorRepository
{
    private readonly AuditDbContext _db;

    public FornecedorRepository(AuditDbContext db)
    {
        _db = db;
    }

    public async Task AddAsync(Fornecedor fornecedor, CancellationToken cancellationToken = default) =>
        await _db.Fornecedores.AddAsync(fornecedor, cancellationToken);

    public Task<Fornecedor?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.Fornecedores.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Fornecedor>> ListarAsync(
        EscopoDono escopo,
        string? busca,
        Guid? reciboId,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Fornecedores.AsNoTracking().AsQueryable();
        if (!escopo.EhAdministrador)
        {
            query = query.Where(x => x.UsuarioId == escopo.UsuarioId);
        }

        if (reciboId.HasValue)
        {
            query = query.Where(x => _db.Compras.Any(c => c.Id == reciboId && c.FornecedorId == x.Id));
        }

        if (!string.IsNullOrWhiteSpace(busca))
        {
            var termo = busca.Trim().ToLower();
            query = query.Where(x =>
                x.Nome.ToLower().Contains(termo) ||
                (x.RazaoSocial != null && x.RazaoSocial.ToLower().Contains(termo)) ||
                (x.CpfCnpj != null && x.CpfCnpj.Contains(termo)));
        }

        return await query.OrderBy(x => x.Nome).ThenByDescending(x => x.DataAtualizacao).ToListAsync(cancellationToken);
    }

    public Task<Fornecedor?> BuscarPorCpfCnpjAsync(Guid usuarioId, string cpfCnpj, CancellationToken cancellationToken = default) =>
        _db.Fornecedores.FirstOrDefaultAsync(x => x.UsuarioId == usuarioId && x.CpfCnpj == cpfCnpj, cancellationToken);

    public Task<Fornecedor?> BuscarPorNomeNormalizadoAsync(
        Guid usuarioId,
        string nomeNormalizado,
        CancellationToken cancellationToken = default) =>
        _db.Fornecedores.FirstOrDefaultAsync(
            x => x.UsuarioId == usuarioId && x.NomeNormalizado == nomeNormalizado,
            cancellationToken);

    public async Task<Fornecedor?> BuscarPorNomeAproximadoAsync(
        Guid usuarioId,
        string nomeNormalizado,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(nomeNormalizado))
        {
            return null;
        }

        var exacto = await _db.Fornecedores.FirstOrDefaultAsync(
            x => x.UsuarioId == usuarioId && x.NomeNormalizado == nomeNormalizado,
            cancellationToken);
        if (exacto is not null)
        {
            return exacto;
        }

        if (FornecedorCorrespondencia.EhNaoIdentificado(nomeNormalizado))
        {
            return null;
        }

        var query = _db.Fornecedores.Where(x =>
            x.UsuarioId == usuarioId
            && x.NomeNormalizado != FornecedorCorrespondencia.NomeNaoIdentificadoNormalizado
            && x.NomeNormalizado != "FORNECEDOR NAO ENCONTRADO");

        if (nomeNormalizado.Length >= 8)
        {
            var termo = nomeNormalizado;
            var porContains = await query
                .Where(x =>
                    x.NomeNormalizado.Contains(termo)
                    || (x.NomeNormalizado.Length >= 8 && termo.Contains(x.NomeNormalizado)))
                .OrderBy(x => Math.Abs(x.NomeNormalizado.Length - termo.Length))
                .ThenBy(x => x.DataCriacao)
                .FirstOrDefaultAsync(cancellationToken);
            if (porContains is not null)
            {
                return porContains;
            }
        }

        var tokens = FornecedorCorrespondencia.TokensSignificativos(nomeNormalizado);
        if (tokens.Count >= 2)
        {
            foreach (var token in tokens)
            {
                var t = token;
                query = query.Where(x => x.NomeNormalizado.Contains(t));
            }

            return await query
                .OrderBy(x => Math.Abs(x.NomeNormalizado.Length - nomeNormalizado.Length))
                .ThenBy(x => x.DataCriacao)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (tokens.Count == 1 && tokens[0].Length >= 5)
        {
            var t = tokens[0];
            return await query
                .Where(x => x.NomeNormalizado.Contains(t))
                .OrderBy(x => Math.Abs(x.NomeNormalizado.Length - nomeNormalizado.Length))
                .ThenBy(x => x.DataCriacao)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return null;
    }

    public Task<int> ContarComprasVinculadasAsync(Guid fornecedorId, CancellationToken cancellationToken = default) =>
        _db.Compras.CountAsync(x => x.FornecedorId == fornecedorId, cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, int>> ContarRecibosAsync(
        IReadOnlyCollection<Guid> fornecedorIds,
        CancellationToken cancellationToken = default)
    {
        if (fornecedorIds.Count == 0)
        {
            return new Dictionary<Guid, int>();
        }

        return await _db.Compras.AsNoTracking()
            .Where(x => x.FornecedorId != null && fornecedorIds.Contains(x.FornecedorId.Value))
            .GroupBy(x => x.FornecedorId!.Value)
            .Select(g => new { Id = g.Key, Quantidade = g.Count() })
            .ToDictionaryAsync(x => x.Id, x => x.Quantidade, cancellationToken);
    }

    public void Remove(Fornecedor fornecedor) => _db.Fornecedores.Remove(fornecedor);
}

public sealed class ProdutoRepository : IProdutoRepository
{
    private readonly AuditDbContext _db;

    public ProdutoRepository(AuditDbContext db)
    {
        _db = db;
    }

    public async Task AddAsync(Produto produto, CancellationToken cancellationToken = default) =>
        await _db.Produtos.AddAsync(produto, cancellationToken);

    public Task<Produto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.Produtos.Include(x => x.Nomes).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Produto>> ListarAsync(
        EscopoDono escopo,
        string? busca,
        Guid? reciboId,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Produtos.AsNoTracking().Include(x => x.Nomes).AsQueryable();
        if (!escopo.EhAdministrador)
        {
            query = query.Where(x => x.UsuarioId == escopo.UsuarioId);
        }

        if (reciboId.HasValue)
        {
            query = query.Where(x => x.ReciboId == reciboId);
        }

        if (!string.IsNullOrWhiteSpace(busca))
        {
            var termo = busca.Trim().ToLower();
            query = query.Where(x =>
                x.Nome.ToLower().Contains(termo) ||
                x.Nomes.Any(n => n.Nome.ToLower().Contains(termo)));
        }

        return await query.OrderByDescending(x => x.DataAtualizacao).ThenBy(x => x.Nome).Take(2000).ToListAsync(cancellationToken);
    }

    public async Task<Produto?> BuscarPorNomeOuSinonimoAsync(
        Guid usuarioId,
        string nomeNormalizado,
        CancellationToken cancellationToken = default)
    {
        var porNome = await _db.Produtos
            .Include(x => x.Nomes)
            .FirstOrDefaultAsync(
                x => x.UsuarioId == usuarioId && x.NomeNormalizado == nomeNormalizado,
                cancellationToken);
        if (porNome is not null)
        {
            return porNome;
        }

        return await _db.Produtos
            .Include(x => x.Nomes)
            .FirstOrDefaultAsync(
                x => x.UsuarioId == usuarioId && x.Nomes.Any(n => n.NomeNormalizado == nomeNormalizado),
                cancellationToken);
    }

    public async Task<int> ContarItensVinculadosAsync(Guid produtoId, CancellationToken cancellationToken = default)
    {
        var itens = await _db.CompraItens.CountAsync(x => x.ProdutoId == produtoId, cancellationToken);
        return itens;
    }

    public void Remove(Produto produto) => _db.Produtos.Remove(produto);
}

public sealed class ServicoRepository : IServicoRepository
{
    private readonly AuditDbContext _db;

    public ServicoRepository(AuditDbContext db)
    {
        _db = db;
    }

    public async Task AddAsync(Servico servico, CancellationToken cancellationToken = default) =>
        await _db.Servicos.AddAsync(servico, cancellationToken);

    public Task<Servico?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.Servicos.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Servico>> ListarAsync(
        EscopoDono escopo,
        string? busca,
        Guid? reciboId,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Servicos.AsNoTracking().AsQueryable();
        if (!escopo.EhAdministrador)
        {
            query = query.Where(x => x.UsuarioId == escopo.UsuarioId);
        }

        if (reciboId.HasValue)
        {
            query = query.Where(x => x.ReciboId == reciboId);
        }

        if (!string.IsNullOrWhiteSpace(busca))
        {
            var termo = busca.Trim().ToLower();
            query = query.Where(x =>
                x.Nome.ToLower().Contains(termo) ||
                (x.CodigoExterno != null && x.CodigoExterno.ToLower().Contains(termo)));
        }

        return await query.OrderByDescending(x => x.DataAtualizacao).ThenBy(x => x.Nome).Take(2000).ToListAsync(cancellationToken);
    }

    public Task<int> ContarItensVinculadosAsync(Guid servicoId, CancellationToken cancellationToken = default) =>
        _db.CompraItens.CountAsync(x => x.ServicoId == servicoId, cancellationToken);

    public void Remove(Servico servico) => _db.Servicos.Remove(servico);
}
