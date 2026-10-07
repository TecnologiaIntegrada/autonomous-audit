using System.Net;
using AutonomousAudit.Application;
using AutonomousAudit.Application.RequestHandlers;
using AutonomousAudit.Application.Security;
using AutonomousAudit.Domain;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using QRCoder;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AutonomousAudit.Api;

public static class ComprasEndpoints
{
    public static WebApplication MapCompras(this WebApplication app)
    {
        var group = app.MapGroup("/v1/recibos").WithTags("Recibos");

        group.MapPost("", CriarComArquivos)
            .DisableAntiforgery()
            .RequireRecurso(RecursoChaves.ReciboCreate)
            .Accepts<CompraAnexoForm>("multipart/form-data")
            .Produces<CompraListaResponse>((int)HttpStatusCode.Accepted);

        group.MapPost("/rascunho", CriarRascunho)
            .RequireRecurso(RecursoChaves.ReciboCreate)
            .Produces<CompraListaResponse>((int)HttpStatusCode.Created);

        group.MapDelete("/rascunhos", LimparRascunhosAnteriores)
            .RequireRecurso(RecursoChaves.ReciboCreate)
            .Produces<LimparRascunhosAnterioresResponse>()
            .WithSummary("Remove rascunhos do usuario criados em dias anteriores")
            .WithDescription(
                "Usado ao abrir Adicionar Recibo / NF. Apaga compras em rascunho (com ou sem arquivo) cuja data de envio e anterior ao inicio do dia atual em America/Sao_Paulo. A captura de hoje permanece.");

        group.MapGet("", Listar)
            .RequireRecurso(RecursoChaves.ReciboRead)
            .Produces<IReadOnlyList<CompraListaResponse>>();

        group.MapGet("/resumo", Resumo)
            .RequireRecurso(RecursoChaves.ReciboRead)
            .Produces<ComprasResumoResponse>();

        group.MapGet("/{id:guid}", Obter)
            .RequireRecurso(RecursoChaves.ReciboRead)
            .Produces<CompraDetalheResponse>()
            .Produces((int)HttpStatusCode.NotFound);

        group.MapPost("/{id:guid}/anexos", Anexar)
            .DisableAntiforgery()
            .RequireRecurso(RecursoChaves.ReciboCreate)
            .Accepts<CompraAnexoForm>("multipart/form-data")
            .Produces<CompraAnexoResponse>((int)HttpStatusCode.Created);

        group.MapPatch("/{id:guid}/anexos", PatchAnexos)
            .RequireRecurso(RecursoChaves.ReciboUpdate)
            .Accepts<PatchAnexosBody>("application/json")
            .Produces<CompraDetalheResponse>();

        group.MapPatch("/{id:guid}/fornecedor", AlterarFornecedor)
            .RequireRecurso(RecursoChaves.ReciboUpdate)
            .Accepts<AlterarFornecedorReciboBody>("application/json")
            .Produces<CompraListaResponse>()
            .WithSummary("Troca o fornecedor do recibo e das compras derivadas do mesmo lote");

        group.MapPost("/{id:guid}/captura", CriarCaptura)
            .RequireRecurso(RecursoChaves.ReciboUpdate)
            .Produces<CapturaCriadaResponse>();

        group.MapGet("/{id:guid}/captura", StatusCaptura)
            .RequireRecurso(RecursoChaves.ReciboRead)
            .Produces<CapturaStatusResponse>();

        group.MapPost("/{id:guid}/processar", Processar)
            .RequireRecurso(RecursoChaves.ReciboProcessar)
            .Produces<CompraListaResponse>();

        group.MapPost("/{id:guid}/reprocessar", Reprocessar)
            .RequireRecurso(RecursoChaves.ReciboProcessar)
            .Produces<CompraListaResponse>();

        group.MapPut("/{id:guid}/validacao", Validar)
            .RequireRecurso(RecursoChaves.ReciboValidar)
            .Accepts<ValidarCompraBody>("application/json")
            .Produces<CompraDetalheResponse>();

        group.MapGet("/{id:guid}/documento", Preview)
            .RequireRecurso(RecursoChaves.ReciboRead)
            .Produces<DocumentoPreviewResponse>();

        group.MapGet("/{id:guid}/arquivo", Arquivo)
            .AllowAnonymous()
            .WithSummary("Baixa o arquivo do recibo")
            .WithDescription("Download do PDF processado. Não exige autenticação: o identificador do recibo na URL é suficiente.")
            .Produces(200, contentType: "application/pdf")
            .Produces((int)HttpStatusCode.NotFound);

        group.MapGet("/{usuarioId:guid}/{reciboId:guid}/{arquivoId:guid}", ArquivoPorIds)
            .AllowAnonymous()
            .WithSummary("Baixa o arquivo do recibo pelo caminho usuario/recibo/arquivo")
            .WithDescription(
                "Download público alinhado ao armazenamento: AutonomousAudit/recibos/{usuarioId}/{reciboId}/{arquivoId}.pdf. " +
                "Usado pela API de relatório (arquivoUrl).")
            .Produces(200, contentType: "application/pdf")
            .Produces((int)HttpStatusCode.NotFound);

        group.MapDelete("/{id:guid}", Excluir)
            .RequireRecurso(RecursoChaves.ReciboDelete)
            .Produces((int)HttpStatusCode.NoContent)
            .Produces((int)HttpStatusCode.Conflict)
            .WithSummary("Exclui o recibo/NF, a compra, arquivos, produtos exclusivos e o fornecedor se nao houver outros recibos");

        MapCapturaPublica(app, "/v1/recibos");
        MapCapturaPublica(app, "/v1/compras");

        var compras = app.MapGroup("/v1/compras").WithTags("Compras");
        compras.MapGet("", ListarLancadas)
            .RequireRecurso(RecursoChaves.ReciboRead)
            .Produces<IReadOnlyList<CompraListaResponse>>()
            .WithSummary("Lista compras lancadas a partir dos recibos");
        compras.MapGet("/resumo", Resumo)
            .RequireRecurso(RecursoChaves.ReciboRead)
            .Produces<ComprasResumoResponse>();
        compras.MapGet("/{id:guid}", Obter)
            .RequireRecurso(RecursoChaves.ReciboRead)
            .Produces<CompraDetalheResponse>()
            .Produces((int)HttpStatusCode.NotFound)
            .WithSummary("Abre uma compra lancada com fornecedor e produtos");
        compras.MapGet("/{id:guid}/documento", Preview)
            .RequireRecurso(RecursoChaves.ReciboRead)
            .Produces<DocumentoPreviewResponse>();

        app.MapGet("/v1/fornecedores", ListarFornecedores)
            .RequireRecurso(RecursoChaves.FornecedorRead)
            .WithTags("Fornecedores")
            .Produces<IReadOnlyList<FornecedorResponse>>();

        app.MapGet("/v1/fornecedores/{id:guid}/recibos", ListarRecibosFornecedor)
            .RequireRecurso(RecursoChaves.FornecedorRead)
            .WithTags("Fornecedores")
            .Produces<IReadOnlyList<CompraListaResponse>>()
            .WithSummary("Lista recibos e notas fiscais vinculados ao fornecedor, na ordem de entrada");

        app.MapPost("/v1/fornecedores", CriarFornecedor)
            .RequireRecurso(RecursoChaves.FornecedorCreate)
            .WithTags("Fornecedores")
            .Produces<FornecedorResponse>((int)HttpStatusCode.Created);

        app.MapPut("/v1/fornecedores/{id:guid}", AtualizarFornecedor)
            .RequireRecurso(RecursoChaves.FornecedorUpdate)
            .WithTags("Fornecedores")
            .Produces<FornecedorResponse>();

        app.MapDelete("/v1/fornecedores/{id:guid}", ExcluirFornecedor)
            .RequireRecurso(RecursoChaves.FornecedorDelete)
            .WithTags("Fornecedores")
            .Produces((int)HttpStatusCode.NoContent);

        app.MapPost("/v1/fornecedores/completar", CompletarFornecedor)
            .RequireRecurso(RecursoChaves.FornecedorUpdate)
            .WithTags("Fornecedores")
            .Accepts<FornecedorBody>("application/json")
            .Produces<FornecedorCompletarResponse>()
            .Produces((int)HttpStatusCode.NotFound)
            .WithRequestTimeout(TimeSpan.FromMinutes(2))
            .WithSummary("Pesquisa dados publicos do fornecedor no Perplexity pelo nome");

        app.MapGet("/v1/produtos", ListarProdutos)
            .RequireRecurso(RecursoChaves.ProdutoRead)
            .WithTags("Produtos")
            .Produces<IReadOnlyList<ProdutoResponse>>();

        app.MapPost("/v1/produtos", CriarProduto)
            .RequireRecurso(RecursoChaves.ProdutoCreate)
            .WithTags("Produtos")
            .Produces<ProdutoResponse>((int)HttpStatusCode.Created);

        app.MapPut("/v1/produtos/{id:guid}", AtualizarProduto)
            .RequireRecurso(RecursoChaves.ProdutoUpdate)
            .WithTags("Produtos")
            .Produces<ProdutoResponse>();

        app.MapDelete("/v1/produtos/{id:guid}", ExcluirProduto)
            .RequireRecurso(RecursoChaves.ProdutoDelete)
            .WithTags("Produtos")
            .Produces((int)HttpStatusCode.NoContent);

        app.MapGet("/v1/servicos", ListarServicos)
            .RequireRecurso(RecursoChaves.ProdutoRead)
            .WithTags("Servicos")
            .Produces<IReadOnlyList<ServicoResponse>>();

        app.MapPost("/v1/servicos", CriarServico)
            .RequireRecurso(RecursoChaves.ProdutoCreate)
            .WithTags("Servicos")
            .Produces<ServicoResponse>((int)HttpStatusCode.Created);

        app.MapPut("/v1/servicos/{id:guid}", AtualizarServico)
            .RequireRecurso(RecursoChaves.ProdutoUpdate)
            .WithTags("Servicos")
            .Produces<ServicoResponse>();

        app.MapDelete("/v1/servicos/{id:guid}", ExcluirServico)
            .RequireRecurso(RecursoChaves.ProdutoDelete)
            .WithTags("Servicos")
            .Produces((int)HttpStatusCode.NoContent);

        app.MapGet("/v1/relatorios/itens", RelatorioItens)
            .RequireRecurso(RecursoChaves.RelatorioRead)
            .WithTags("Relatorios")
            .WithSummary("Lista compras, fornecedor e produtos do usuario")
            .WithDescription(
                "Devolve as compras extraidas dos recibos do usuario autenticado, na hierarquia compra > fornecedor > produtos, com URL do PDF. Rascunhos sem processamento (Aguardando envio) nao entram. Periodo maximo de 3 meses, ate 10.000 registros. Use formato=json (padrao), csv ou xlsx.")
            .Produces<RelatorioItensResponse>();

        return app;
    }

    private static void MapCapturaPublica(WebApplication app, string prefix)
    {
        app.MapPost($"{prefix}/captura/{{token}}/abrir", AbrirCaptura)
            .AllowAnonymous()
            .WithTags("Recibos")
            .Produces<CapturaAbertaResponse>();

        app.MapPost($"{prefix}/captura/{{token}}/anexos", AnexarCaptura)
            .AllowAnonymous()
            .DisableAntiforgery()
            .WithTags("Recibos")
            .Accepts<CompraAnexoForm>("multipart/form-data")
            .Produces<CompraAnexoResponse>((int)HttpStatusCode.Created);

        app.MapPost($"{prefix}/captura/{{token}}/concluir", ConcluirCaptura)
            .AllowAnonymous()
            .WithTags("Recibos");
    }

    private static async Task<IResult> CriarComArquivos(HttpContext http, IMediator mediator, CancellationToken ct)
    {
        if (!TryUsuario(http, out var usuarioId, out var erro))
        {
            return erro;
        }

        if (!http.Request.HasFormContentType)
        {
            return Problem(400, "Envie as imagens ou o PDF em multipart/form-data.");
        }

        var entradas = new List<ReciboArquivoEntrada>();
        foreach (var arquivo in http.Request.Form.Files)
        {
            if (arquivo.Length == 0)
            {
                continue;
            }

            await using var buffer = new MemoryStream();
            await arquivo.CopyToAsync(buffer, ct);
            entradas.Add(new ReciboArquivoEntrada(
                arquivo.FileName,
                arquivo.ContentType ?? "",
                buffer.ToArray()));
        }

        var resultado = await mediator.Send(new CriarReciboComAnexosCommand(usuarioId, entradas), ct);
        return resultado switch
        {
            CriarReciboComAnexosOk ok => Results.Accepted($"/v1/recibos/{ok.Compra.Id}", CompraListaResponse.From(ok.Compra)),
            CriarReciboComAnexosBadRequest bad => Problem(400, bad.Message),
            _ => Problem(400, "Nao foi possivel criar o recibo.")
        };
    }

    private static async Task<IResult> CriarRascunho(HttpContext http, IMediator mediator, CancellationToken ct)
    {
        if (!TryUsuario(http, out var usuarioId, out var erro))
        {
            return erro;
        }

        var resultado = await mediator.Send(new CriarCompraCommand(usuarioId), ct);
        return Results.Created($"/v1/recibos/{resultado.Compra.Id}", CompraListaResponse.From(resultado.Compra));
    }

    private static async Task<IResult> LimparRascunhosAnteriores(HttpContext http, IMediator mediator, CancellationToken ct)
    {
        if (!TryUsuario(http, out var usuarioId, out var erro))
        {
            return erro;
        }

        var resultado = await mediator.Send(new LimparRascunhosAnterioresCommand(usuarioId), ct);
        return Results.Ok(new LimparRascunhosAnterioresResponse(resultado.Removidos));
    }

    private static async Task<IResult> Listar(HttpContext http, string? status, IMediator mediator, CancellationToken ct) =>
        await ListarInterno(http, status, lancadas: false, mediator, ct);

    private static async Task<IResult> ListarLancadas(HttpContext http, string? status, IMediator mediator, CancellationToken ct) =>
        await ListarInterno(http, status, lancadas: true, mediator, ct);

    private static async Task<IResult> ListarInterno(
        HttpContext http,
        string? status,
        bool lancadas,
        IMediator mediator,
        CancellationToken ct)
    {
        if (!TryUsuario(http, out var usuarioId, out var erro))
        {
            return erro;
        }

        var resultado = await mediator.Send(new ListarComprasQuery(usuarioId, status, SomenteOrigem: !lancadas), ct);
        IEnumerable<Compra> compras = resultado.Compras;
        if (lancadas)
        {
            compras = compras.Where(c =>
                c.Status is CompraStatus.Processado
                    or CompraStatus.Revisao
                    or CompraStatus.Validada
                    or CompraStatus.Concluida);
        }

        return Results.Ok(compras.Select(c => CompraListaResponse.From(c, BasePublica(http))).ToList());
    }

    private static async Task<IResult> Resumo(
        HttpContext http,
        int? ano,
        int? mes,
        Guid? fornecedorId,
        IMediator mediator,
        CancellationToken ct)
    {
        if (!TryUsuario(http, out var usuarioId, out var erro))
        {
            return erro;
        }

        var (anoPadrao, mesPadrao) = PeriodoBrasil.MesVigente();
        var anoFiltro = ano is > 2000 and < 2100 ? ano.Value : anoPadrao;
        var mesFiltro = mes is >= 1 and <= 12 ? mes.Value : mesPadrao;
        var resultado = await mediator.Send(new ResumoComprasQuery(usuarioId, anoFiltro, mesFiltro, fornecedorId), ct);
        var r = resultado.Resumo;
        return Results.Ok(new ComprasResumoResponse(
            r.TotalCompras,
            r.Rascunhos,
            r.Processando,
            r.EmRevisao,
            r.Validadas,
            r.Concluidas,
            r.Falhas,
            r.TotalGasto,
            r.PorFornecedor.Select(x => new ComprasResumoFornecedorResponse(x.FornecedorId, x.Nome, x.Quantidade, x.Total)).ToList(),
            r.Processados,
            resultado.ArmazenamentoUsadoBytes,
            resultado.ArmazenamentoLimiteBytes,
            resultado.QwenTokensTotal,
            resultado.QwenCustoTotalBrl,
            resultado.PerplexityTokensTotal,
            resultado.PerplexityCustoTotalBrl,
            resultado.Ano,
            resultado.Mes));
    }

    private static async Task<IResult> Obter(HttpContext http, Guid id, IMediator mediator, CancellationToken ct)
    {
        if (!TryUsuario(http, out var usuarioId, out var erro))
        {
            return erro;
        }

        var resultado = await mediator.Send(new ObterCompraQuery(usuarioId, id), ct);
        return resultado switch
        {
            ObterCompraOk ok => Results.Ok(CompraDetalheResponse.From(ok.Compra, ok.Duplicatas, BasePublica(http))),
            ObterCompraForbidden => Problem(403, "Sem permissao para este recibo."),
            _ => Problem(404, "Recibo nao encontrado.")
        };
    }

    private static async Task<IResult> Anexar(
        HttpContext http,
        Guid id,
        IFormFile? arquivo,
        IMediator mediator,
        CancellationToken ct)
    {
        if (!TryUsuario(http, out var usuarioId, out var erro))
        {
            return erro;
        }

        if (arquivo is null || arquivo.Length == 0)
        {
            return Problem(400, "Envie o arquivo no campo multipart 'arquivo'.");
        }

        await using var stream = arquivo.OpenReadStream();
        var resultado = await mediator.Send(
            new AnexarCompraCommand(usuarioId, id, arquivo.FileName, arquivo.ContentType ?? "", arquivo.Length, stream, ""),
            ct);
        return resultado switch
        {
            AnexarCompraOk ok => Results.Created($"/v1/compras/{id}/anexos/{ok.Anexo.Id}", CompraAnexoResponse.From(ok.Anexo)),
            AnexarCompraForbidden => Problem(403, "Sem permissao para esta compra."),
            AnexarCompraBadRequest bad => Problem(400, bad.Message),
            _ => Problem(404, "Compra nao encontrada.")
        };
    }

    private static async Task<IResult> PatchAnexos(
        HttpContext http,
        Guid id,
        PatchAnexosBody body,
        IMediator mediator,
        CancellationToken ct)
    {
        if (!TryUsuario(http, out var usuarioId, out var erro))
        {
            return erro;
        }

        var resultado = await mediator.Send(new PatchAnexosCompraCommand(usuarioId, id, body.Ordem, body.Remover), ct);
        return resultado switch
        {
            PatchAnexosCompraOk ok => Results.Ok(CompraDetalheResponse.From(ok.Compra, [], BasePublica(http))),
            PatchAnexosCompraForbidden => Problem(403, "Sem permissao para esta compra."),
            PatchAnexosCompraBadRequest bad => Problem(400, bad.Message),
            _ => Problem(404, "Compra nao encontrada.")
        };
    }

    private static async Task<IResult> AlterarFornecedor(
        HttpContext http,
        Guid id,
        AlterarFornecedorReciboBody body,
        IMediator mediator,
        CancellationToken ct)
    {
        if (!TryUsuario(http, out var usuarioId, out var erro))
        {
            return erro;
        }

        if (body.FornecedorId == Guid.Empty)
        {
            return Problem(400, "Informe o fornecedor ou prestador.");
        }

        var resultado = await mediator.Send(
            new AlterarFornecedorReciboCommand(usuarioId, id, body.FornecedorId),
            ct);
        return resultado switch
        {
            AlterarFornecedorReciboOk ok => Results.Ok(CompraListaResponse.From(ok.Compra, BasePublica(http))),
            AlterarFornecedorReciboForbidden => Problem(403, "Sem permissao para este recibo."),
            AlterarFornecedorReciboBadRequest bad => Problem(400, bad.Message),
            _ => Problem(404, "Recibo ou nota fiscal nao encontrado.")
        };
    }

    private static async Task<IResult> CriarCaptura(HttpContext http, Guid id, IMediator mediator, CancellationToken ct)
    {
        if (!TryUsuario(http, out var usuarioId, out var erro))
        {
            return erro;
        }

        var resultado = await mediator.Send(new CriarCapturaCommand(usuarioId, id), ct);
        return resultado switch
        {
            CriarCapturaOk ok => Results.Ok(new CapturaCriadaResponse(ok.Url, ok.ExpiraEm, QrPng.Base64(ok.Url))),
            CriarCapturaForbidden => Problem(403, "Sem permissao para esta compra."),
            CriarCapturaBadRequest bad => Problem(400, bad.Message),
            _ => Problem(404, "Compra nao encontrada.")
        };
    }

    private static async Task<IResult> StatusCaptura(HttpContext http, Guid id, IMediator mediator, CancellationToken ct)
    {
        if (!TryUsuario(http, out var usuarioId, out var erro))
        {
            return erro;
        }

        var resultado = await mediator.Send(new StatusCapturaQuery(usuarioId, id), ct);
        return resultado switch
        {
            StatusCapturaOk ok => Results.Ok(new CapturaStatusResponse(ok.Ativa, ok.Consumida, ok.ExpiraEm, ok.Anexos, ok.Concluida)),
            StatusCapturaForbidden => Problem(403, "Sem permissao para esta compra."),
            _ => Problem(404, "Compra nao encontrada.")
        };
    }

    private static async Task<IResult> Processar(HttpContext http, Guid id, IMediator mediator, CancellationToken ct)
    {
        if (!TryUsuario(http, out var usuarioId, out var erro))
        {
            return erro;
        }

        var resultado = await mediator.Send(new EnfileirarProcessamentoCommand(usuarioId, id, false), ct);
        return MapEnfileirar(resultado);
    }

    private static async Task<IResult> Reprocessar(HttpContext http, Guid id, IMediator mediator, CancellationToken ct)
    {
        if (!TryUsuario(http, out var usuarioId, out var erro))
        {
            return erro;
        }

        var resultado = await mediator.Send(new EnfileirarProcessamentoCommand(usuarioId, id, true), ct);
        return MapEnfileirar(resultado);
    }

    private static IResult MapEnfileirar(EnfileirarProcessamentoResult resultado) => resultado switch
    {
        EnfileirarProcessamentoOk ok => Results.Accepted($"/v1/recibos/{ok.Compra.Id}", CompraListaResponse.From(ok.Compra)),
        EnfileirarProcessamentoForbidden => Problem(403, "Sem permissao para este recibo."),
        EnfileirarProcessamentoBadRequest bad => Problem(400, bad.Message),
        _ => Problem(404, "Recibo nao encontrado.")
    };

    private static async Task<IResult> Validar(
        HttpContext http,
        Guid id,
        ValidarCompraBody body,
        IMediator mediator,
        CancellationToken ct)
    {
        if (!TryUsuario(http, out var usuarioId, out var erro))
        {
            return erro;
        }

        var comando = new ValidarCompraCommand(
            usuarioId,
            id,
            body.DataCompra,
            body.NumeroRecibo,
            body.Subtotal,
            body.Descontos,
            body.Acrescimos,
            body.Total,
            body.FormaPagamento,
            new ValidarFornecedorBody(
                body.Fornecedor.Id,
                body.Fornecedor.Nome,
                body.Fornecedor.RazaoSocial,
                body.Fornecedor.CpfCnpj,
                body.Fornecedor.Telefone,
                body.Fornecedor.Endereco),
            body.Itens.Select(i => new ValidarItemBody(
                i.Id,
                i.Descricao,
                i.Codigo,
                i.Quantidade,
                i.Unidade,
                i.PrecoUnitario,
                i.Desconto,
                i.Total,
                i.ProdutoId,
                i.ProdutoNovo is null
                    ? null
                    : new ValidarProdutoNovoBody(
                        i.ProdutoNovo.Nome,
                        i.ProdutoNovo.Marca,
                        i.ProdutoNovo.Variante,
                        i.ProdutoNovo.UnidadeControle,
                        i.ProdutoNovo.ConteudoEmbalagem))).ToList());

        var resultado = await mediator.Send(comando, ct);
        return resultado switch
        {
            ValidarCompraOk ok => Results.Ok(CompraDetalheResponse.From(ok.Compra, [], BasePublica(http))),
            ValidarCompraForbidden => Problem(403, "Sem permissao para esta compra."),
            ValidarCompraBadRequest bad => Problem(400, bad.Message),
            _ => Problem(404, "Compra nao encontrada.")
        };
    }

    private static async Task<IResult> Preview(HttpContext http, Guid id, IMediator mediator, CancellationToken ct)
    {
        if (!TryUsuario(http, out var usuarioId, out var erro))
        {
            return erro;
        }

        var resultado = await mediator.Send(new PreviewDocumentoQuery(usuarioId, id), ct);
        return resultado switch
        {
            PreviewDocumentoOk ok => Results.Ok(new DocumentoPreviewResponse(ok.Url)),
            PreviewDocumentoForbidden => Problem(403, "Sem permissao para esta compra."),
            _ => Problem(404, "Documento ainda nao enviado ao armazenamento.")
        };
    }

    private static async Task<IResult> Arquivo(Guid id, IMediator mediator, CancellationToken ct)
    {
        var resultado = await mediator.Send(new BaixarDocumentoQuery(id), ct);
        return resultado switch
        {
            BaixarDocumentoOk ok => Results.File(ok.Bytes, "application/pdf", fileDownloadName: ok.NomeArquivo),
            BaixarDocumentoForbidden => Problem(403, "Sem permissao para este recibo."),
            _ => Problem(404, "Previa indisponivel ate que o arquivo seja processado.")
        };
    }

    private static async Task<IResult> ArquivoPorIds(
        Guid usuarioId,
        Guid reciboId,
        Guid arquivoId,
        IMediator mediator,
        CancellationToken ct)
    {
        var resultado = await mediator.Send(new BaixarDocumentoQuery(reciboId, usuarioId, arquivoId), ct);
        return resultado switch
        {
            BaixarDocumentoOk ok => Results.File(ok.Bytes, "application/pdf", fileDownloadName: ok.NomeArquivo),
            BaixarDocumentoForbidden => Problem(403, "Sem permissao para este recibo."),
            _ => Problem(404, "Arquivo do recibo nao encontrado.")
        };
    }

    private static async Task<IResult> Excluir(HttpContext http, Guid id, IMediator mediator, CancellationToken ct)
    {
        if (!TryUsuario(http, out var usuarioId, out var erro))
        {
            return erro;
        }

        var resultado = await mediator.Send(new ExcluirReciboCommand(usuarioId, id), ct);
        return resultado switch
        {
            ExcluirReciboOk => Results.NoContent(),
            ExcluirReciboForbidden => Problem(403, "Sem permissao para este recibo."),
            ExcluirReciboBadRequest bad => Problem(409, bad.Message),
            _ => Problem(404, "Recibo nao encontrado.")
        };
    }

    private static async Task<IResult> RelatorioItens(
        HttpContext http,
        DateTimeOffset? inicio,
        DateTimeOffset? fim,
        Guid? fornecedorId,
        int? limite,
        string? formato,
        IMediator mediator,
        CancellationToken ct)
    {
        if (!TryUsuario(http, out var usuarioId, out var erro))
        {
            return erro;
        }

        var fimEfetivo = fim ?? DateTimeOffset.UtcNow;
        var inicioEfetivo = inicio ?? fimEfetivo.AddDays(-30);
        var periodoInvalido = RelatorioItensHandler.ValidarPeriodo(inicioEfetivo, fimEfetivo);
        if (periodoInvalido is not null)
        {
            return Problem(400, periodoInvalido);
        }

        var resultado = await mediator.Send(
            new RelatorioItensQuery(
                usuarioId,
                inicioEfetivo,
                fimEfetivo,
                fornecedorId,
                limite ?? RelatorioItensHandler.LimiteMaximo),
            ct);
        var corpo = RelatorioItensResponse.From(resultado, inicioEfetivo, fimEfetivo, fornecedorId, BasePublica(http));
        var tipo = (formato ?? "json").Trim().ToLowerInvariant();
        if (tipo is "xlsx" or "excel")
        {
            var bytes = RelatorioPlanilha.Xlsx("Relatorio", RelatorioItensResponse.CabecalhoPlanilha, corpo.LinhasPlanilha());
            return Results.File(
                bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "relatorio-compras.xlsx");
        }

        if (tipo == "csv")
        {
            var bytes = RelatorioPlanilha.Csv(RelatorioItensResponse.CabecalhoPlanilha, corpo.LinhasPlanilha());
            return Results.File(bytes, "text/csv; charset=utf-8", "relatorio-compras.csv");
        }

        return Results.Ok(corpo);
    }

    private static async Task<IResult> AbrirCaptura(string token, IMediator mediator, CancellationToken ct)
    {
        var resultado = await mediator.Send(new AbrirCapturaCommand(token), ct);
        return resultado switch
        {
            AbrirCapturaOk ok => Results.Ok(new CapturaAbertaResponse(ok.CompraId, ok.SessionToken, ok.ExpiraEm)),
            AbrirCapturaBadRequest bad => Problem(400, bad.Message),
            _ => Problem(400, "QR invalido.")
        };
    }

    private static async Task<IResult> AnexarCaptura(
        HttpContext http,
        string token,
        IFormFile? arquivo,
        IMediator mediator,
        CancellationToken ct)
    {
        var sessao = http.Request.Headers["X-Captura-Sessao"].ToString();
        if (string.IsNullOrWhiteSpace(sessao))
        {
            return Problem(401, "Informe o cabecalho X-Captura-Sessao.");
        }

        if (arquivo is null || arquivo.Length == 0)
        {
            return Problem(400, "Envie o arquivo no campo multipart 'arquivo'.");
        }

        await using var stream = arquivo.OpenReadStream();
        var resultado = await mediator.Send(
            new AnexarCapturaPublicaCommand(token, sessao, arquivo.FileName, arquivo.ContentType ?? "", arquivo.Length, stream),
            ct);
        return resultado switch
        {
            AnexarCompraOk ok => Results.Created($"/v1/compras/captura/{token}/anexos/{ok.Anexo.Id}", CompraAnexoResponse.From(ok.Anexo)),
            AnexarCompraBadRequest bad => Problem(400, bad.Message),
            _ => Problem(404, "Captura nao encontrada.")
        };
    }

    private static async Task<IResult> ConcluirCaptura(HttpContext http, string token, IMediator mediator, CancellationToken ct)
    {
        var sessao = http.Request.Headers["X-Captura-Sessao"].ToString();
        if (string.IsNullOrWhiteSpace(sessao))
        {
            return Problem(401, "Informe o cabecalho X-Captura-Sessao.");
        }

        var resultado = await mediator.Send(new ConcluirCapturaCommand(token, sessao), ct);
        return resultado switch
        {
            ConcluirCapturaOk => Results.NoContent(),
            ConcluirCapturaBadRequest bad => Problem(400, bad.Message),
            _ => Problem(400, "Sessao invalida.")
        };
    }

    private static async Task<IResult> ListarFornecedores(HttpContext http, string? busca, Guid? reciboId, bool? somenteProprio, IMediator mediator, CancellationToken ct)
    {
        if (!TryUsuario(http, out var usuarioId, out var erro))
        {
            return erro;
        }

        var resultado = await mediator.Send(
            new ListarFornecedoresQuery(usuarioId, busca, reciboId, somenteProprio == true),
            ct);
        return Results.Ok(resultado.Fornecedores
            .Select(f => FornecedorResponse.From(f, resultado.QuantidadeRecibos.GetValueOrDefault(f.Id)))
            .ToList());
    }

    private static async Task<IResult> ListarRecibosFornecedor(HttpContext http, Guid id, IMediator mediator, CancellationToken ct)
    {
        if (!TryUsuario(http, out var usuarioId, out var erro))
        {
            return erro;
        }

        var resultado = await mediator.Send(new ListarRecibosFornecedorQuery(usuarioId, id), ct);
        return resultado switch
        {
            ListarRecibosFornecedorOk ok => Results.Ok(ok.Recibos.Select(c => CompraListaResponse.From(c, BasePublica(http))).ToList()),
            ListarRecibosFornecedorForbidden => Problem(403, "Sem permissao para este cadastro."),
            _ => Problem(404, "Fornecedor ou prestador nao encontrado.")
        };
    }

    private static async Task<IResult> CriarFornecedor(HttpContext http, FornecedorBody body, IMediator mediator, CancellationToken ct)
    {
        if (!TryUsuario(http, out var usuarioId, out var erro))
        {
            return erro;
        }

        var resultado = await mediator.Send(
            new CriarFornecedorCommand(
                usuarioId,
                body.Nome,
                body.RazaoSocial,
                body.CpfCnpj,
                body.Telefone,
                body.Endereco),
            ct);
        return resultado switch
        {
            SalvarFornecedorOk ok => Results.Created($"/v1/fornecedores/{ok.Fornecedor.Id}", FornecedorResponse.From(ok.Fornecedor, 0)),
            SalvarFornecedorBadRequest bad => Problem(400, bad.Message),
            _ => Problem(400, "Nao foi possivel cadastrar.")
        };
    }

    private static async Task<IResult> AtualizarFornecedor(
        HttpContext http,
        Guid id,
        FornecedorBody body,
        IMediator mediator,
        CancellationToken ct)
    {
        if (!TryUsuario(http, out var usuarioId, out var erro))
        {
            return erro;
        }

        var resultado = await mediator.Send(
            new AtualizarFornecedorCommand(
                usuarioId,
                id,
                body.Nome,
                body.RazaoSocial,
                body.CpfCnpj,
                body.Telefone,
                body.Endereco),
            ct);
        return resultado switch
        {
            SalvarFornecedorOk ok => Results.Ok(FornecedorResponse.From(ok.Fornecedor)),
            SalvarFornecedorForbidden => Problem(403, "Sem permissao para este cadastro."),
            SalvarFornecedorBadRequest bad => Problem(400, bad.Message),
            _ => Problem(404, "Fornecedor ou prestador nao encontrado.")
        };
    }

    private static async Task<IResult> ExcluirFornecedor(HttpContext http, Guid id, IMediator mediator, CancellationToken ct)
    {
        if (!TryUsuario(http, out var usuarioId, out var erro))
        {
            return erro;
        }

        var resultado = await mediator.Send(new ExcluirFornecedorCommand(usuarioId, id), ct);
        return MapExcluirCadastro(resultado, "Fornecedor ou prestador nao encontrado.");
    }

    private static async Task<IResult> CompletarFornecedor(
        HttpContext http,
        FornecedorBody body,
        IMediator mediator,
        CancellationToken ct)
    {
        if (!TryUsuario(http, out var usuarioId, out var erro))
        {
            return erro;
        }

        var resultado = await mediator.Send(
            new CompletarFornecedorCommand(usuarioId, body.Nome),
            ct);
        return resultado switch
        {
            CompletarFornecedorOk ok => Results.Ok(new FornecedorCompletarResponse(
                ok.RazaoSocial,
                ok.CpfCnpj,
                ok.Telefone,
                ok.Endereco,
                ok.Citacoes,
                ok.CamposPreenchidos)),
            CompletarFornecedorBadRequest bad => Problem(400, bad.Message),
            CompletarFornecedorNotFound notFound => Problem(404, notFound.Message),
            CompletarFornecedorUnauthorized unauthorized => Problem(502, unauthorized.Message),
            CompletarFornecedorFail failed => Problem(502, failed.Message),
            _ => Problem(502, "Nao foi possivel completar o fornecedor.")
        };
    }

    private static async Task<IResult> ListarProdutos(HttpContext http, string? busca, Guid? reciboId, IMediator mediator, CancellationToken ct)
    {
        if (!TryUsuario(http, out var usuarioId, out var erro))
        {
            return erro;
        }

        var resultado = await mediator.Send(new ListarProdutosQuery(usuarioId, busca, reciboId), ct);
        return Results.Ok(resultado.Produtos.Select(ProdutoResponse.From).ToList());
    }

    private static async Task<IResult> CriarProduto(HttpContext http, ProdutoBody body, IMediator mediator, CancellationToken ct)
    {
        if (!TryUsuario(http, out var usuarioId, out var erro))
        {
            return erro;
        }

        var resultado = await mediator.Send(
            new CriarProdutoCommand(
                usuarioId,
                body.Nome,
                body.Marca,
                body.Variante,
                body.UnidadeControle,
                body.ConteudoEmbalagem,
                body.Sinonimo,
                body.Sinonimos,
                body.CodigoExterno,
                body.NcmSh,
                body.Csosn,
                body.Cfop,
                body.ValorUnitario,
                body.ValorDesconto,
                body.ValorLiquido,
                body.BaseIcms,
                body.ValorIcms,
                body.ValorIpi,
                body.AliqIcms,
                body.AliqIpi,
                ReciboOpcional(body.ReciboId)),
            ct);
        return resultado switch
        {
            SalvarProdutoOk ok => Results.Created($"/v1/produtos/{ok.Produto.Id}", ProdutoResponse.From(ok.Produto)),
            SalvarProdutoBadRequest bad => Problem(400, bad.Message),
            _ => Problem(400, "Nao foi possivel cadastrar o produto.")
        };
    }

    private static async Task<IResult> AtualizarProduto(
        HttpContext http,
        Guid id,
        ProdutoBody body,
        IMediator mediator,
        CancellationToken ct)
    {
        if (!TryUsuario(http, out var usuarioId, out var erro))
        {
            return erro;
        }

        var resultado = await mediator.Send(
            new AtualizarProdutoCommand(
                usuarioId,
                id,
                body.Nome,
                body.Marca,
                body.Variante,
                body.UnidadeControle,
                body.ConteudoEmbalagem,
                body.Sinonimo,
                body.Sinonimos,
                body.CodigoExterno,
                body.NcmSh,
                body.Csosn,
                body.Cfop,
                body.ValorUnitario,
                body.ValorDesconto,
                body.ValorLiquido,
                body.BaseIcms,
                body.ValorIcms,
                body.ValorIpi,
                body.AliqIcms,
                body.AliqIpi,
                ReciboOpcional(body.ReciboId)),
            ct);
        return resultado switch
        {
            SalvarProdutoOk ok => Results.Ok(ProdutoResponse.From(ok.Produto)),
            SalvarProdutoForbidden => Problem(403, "Sem permissao para este produto."),
            SalvarProdutoBadRequest bad => Problem(400, bad.Message),
            _ => Problem(404, "Produto nao encontrado.")
        };
    }

    private static async Task<IResult> ExcluirProduto(HttpContext http, Guid id, IMediator mediator, CancellationToken ct)
    {
        if (!TryUsuario(http, out var usuarioId, out var erro))
        {
            return erro;
        }

        var resultado = await mediator.Send(new ExcluirProdutoCommand(usuarioId, id), ct);
        return MapExcluirCadastro(resultado, "Produto nao encontrado.");
    }

    private static async Task<IResult> ListarServicos(HttpContext http, string? busca, Guid? reciboId, IMediator mediator, CancellationToken ct)
    {
        if (!TryUsuario(http, out var usuarioId, out var erro))
        {
            return erro;
        }

        var resultado = await mediator.Send(new ListarServicosQuery(usuarioId, busca, reciboId), ct);
        return Results.Ok(resultado.Servicos.Select(ServicoResponse.From).ToList());
    }

    private static async Task<IResult> CriarServico(HttpContext http, ServicoBody body, IMediator mediator, CancellationToken ct)
    {
        if (!TryUsuario(http, out var usuarioId, out var erro))
        {
            return erro;
        }

        var resultado = await mediator.Send(
            new CriarServicoCommand(
                usuarioId,
                body.Nome,
                body.UnidadeControle,
                body.CodigoExterno,
                body.NcmSh,
                body.Csosn,
                body.Cfop,
                body.ValorUnitario,
                body.ValorDesconto,
                body.ValorLiquido,
                body.BaseIcms,
                body.ValorIcms,
                body.ValorIpi,
                body.AliqIcms,
                body.AliqIpi,
                ReciboOpcional(body.ReciboId)),
            ct);
        return resultado switch
        {
            SalvarServicoOk ok => Results.Created($"/v1/servicos/{ok.Servico.Id}", ServicoResponse.From(ok.Servico)),
            SalvarServicoBadRequest bad => Problem(400, bad.Message),
            _ => Problem(400, "Nao foi possivel cadastrar o servico.")
        };
    }

    private static async Task<IResult> AtualizarServico(
        HttpContext http,
        Guid id,
        ServicoBody body,
        IMediator mediator,
        CancellationToken ct)
    {
        if (!TryUsuario(http, out var usuarioId, out var erro))
        {
            return erro;
        }

        var resultado = await mediator.Send(
            new AtualizarServicoCommand(
                usuarioId,
                id,
                body.Nome,
                body.UnidadeControle,
                body.CodigoExterno,
                body.NcmSh,
                body.Csosn,
                body.Cfop,
                body.ValorUnitario,
                body.ValorDesconto,
                body.ValorLiquido,
                body.BaseIcms,
                body.ValorIcms,
                body.ValorIpi,
                body.AliqIcms,
                body.AliqIpi,
                ReciboOpcional(body.ReciboId)),
            ct);
        return resultado switch
        {
            SalvarServicoOk ok => Results.Ok(ServicoResponse.From(ok.Servico)),
            SalvarServicoForbidden => Problem(403, "Sem permissao para este servico."),
            SalvarServicoBadRequest bad => Problem(400, bad.Message),
            _ => Problem(404, "Servico nao encontrado.")
        };
    }

    private static async Task<IResult> ExcluirServico(HttpContext http, Guid id, IMediator mediator, CancellationToken ct)
    {
        if (!TryUsuario(http, out var usuarioId, out var erro))
        {
            return erro;
        }

        var resultado = await mediator.Send(new ExcluirServicoCommand(usuarioId, id), ct);
        return MapExcluirCadastro(resultado, "Servico nao encontrado.");
    }

    private static IResult MapExcluirCadastro(ExcluirCadastroResult resultado, string naoEncontrado) => resultado switch
    {
        ExcluirCadastroOk => Results.NoContent(),
        ExcluirCadastroForbidden => Problem(403, "Sem permissao para este cadastro."),
        ExcluirCadastroConflict conflict => Problem(409, conflict.Message),
        _ => Problem(404, naoEncontrado)
    };

    private static Guid? ReciboOpcional(Guid? reciboId) =>
        reciboId is null || reciboId == Guid.Empty ? null : reciboId;

    private static bool TryUsuario(HttpContext http, out Guid usuarioId, out IResult erro)
    {
        if (http.Items[RecursoAuthorization.HttpItemUsuarioId] is Guid id)
        {
            usuarioId = id;
            erro = Results.Empty;
            return true;
        }

        usuarioId = Guid.Empty;
        erro = Problem(401, "Usuario autenticado nao identificado.");
        return false;
    }

    private static string BasePublica(HttpContext http)
    {
        var proto = http.Request.Headers["X-Forwarded-Proto"].ToString();
        var scheme = string.IsNullOrWhiteSpace(proto)
            ? http.Request.Scheme
            : proto.Split(',')[0].Trim();
        if (string.IsNullOrWhiteSpace(scheme))
        {
            scheme = "https";
        }

        var forwardedHost = http.Request.Headers["X-Forwarded-Host"].ToString();
        var host = string.IsNullOrWhiteSpace(forwardedHost)
            ? http.Request.Host.Value
            : forwardedHost.Split(',')[0].Trim();
        return $"{scheme}://{host}".TrimEnd('/');
    }

    private static IResult Problem(int status, string detalhe) =>
        Results.Problem(
            title: status == 401 ? "Nao autorizado" : status == 403 ? "Permissao insuficiente" : status == 404 ? "Nao encontrado" : "Requisicao invalida",
            detail: detalhe,
            statusCode: status);
}

internal static class QrPng
{
    public static string Base64(string url)
    {
        using var gerador = new QRCodeGenerator();
        using var dados = gerador.CreateQrCode(url, QRCodeGenerator.ECCLevel.Q);
        var png = new PngByteQRCode(dados);
        return Convert.ToBase64String(png.GetGraphic(8));
    }
}

public sealed class CompraAnexoForm
{
    public IFormFile? Arquivo { get; set; }
}

public sealed record PatchAnexosBody(IReadOnlyList<Guid>? Ordem, IReadOnlyList<Guid>? Remover);
public sealed record AlterarFornecedorReciboBody(Guid FornecedorId);

public sealed record ValidarCompraBody(
    DateTimeOffset? DataCompra,
    string? NumeroRecibo,
    decimal? Subtotal,
    decimal? Descontos,
    decimal? Acrescimos,
    decimal? Total,
    string? FormaPagamento,
    ValidarFornecedorHttp Fornecedor,
    IReadOnlyList<ValidarItemHttp> Itens);

public sealed record ValidarFornecedorHttp(
    Guid? Id,
    string Nome,
    string? RazaoSocial,
    string? CpfCnpj,
    string? Telefone,
    string? Endereco);

public sealed record ValidarItemHttp(
    Guid? Id,
    string Descricao,
    string? Codigo,
    decimal Quantidade,
    string? Unidade,
    decimal? PrecoUnitario,
    decimal? Desconto,
    decimal? Total,
    Guid? ProdutoId,
    ValidarProdutoNovoHttp? ProdutoNovo);

public sealed record ValidarProdutoNovoHttp(
    string Nome,
    string? Marca,
    string? Variante,
    string? UnidadeControle,
    decimal? ConteudoEmbalagem);

public sealed record FornecedorBody(
    string Nome,
    string? RazaoSocial,
    string? CpfCnpj,
    string? Telefone,
    string? Endereco,
    Guid? ReciboId = null);
public sealed record ProdutoBody(
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
    Guid? ReciboId = null);

public sealed record ServicoBody(
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
    Guid? ReciboId = null);

public sealed record FornecedorCompletarResponse(
    string? RazaoSocial,
    string? CpfCnpj,
    string? Telefone,
    string? Endereco,
    IReadOnlyList<string> Citacoes,
    IReadOnlyList<string> CamposPreenchidos);

public sealed record CompraListaResponse(
    Guid Id,
    string Codigo,
    string Status,
    DateTimeOffset DataEnvio,
    DateTimeOffset? DataCompra,
    string? NumeroRecibo,
    decimal? Subtotal,
    decimal? Descontos,
    decimal? Acrescimos,
    decimal? Total,
    string? FormaPagamento,
    Guid? FornecedorId,
    string? FornecedorNome,
    string? FornecedorRazaoSocial,
    string? FornecedorCpfCnpj,
    string? FornecedorTelefone,
    string? FornecedorEndereco,
    string? Paginas,
    string TipoDocumento,
    string TipoItem,
    int? DanfeTipo,
    string? Serie,
    string? ChaveAcesso,
    Guid? ReciboOrigemId,
    string? ArquivoUrl,
    string? UltimoErro,
    JsonElement? QwenJson)
{
    [JsonPropertyName("geminiJson")]
    public JsonElement? GeminiJson => QwenJson;

    [JsonPropertyName("perplexityJson")]
    public JsonElement? PerplexityJson => QwenJson;

    public static CompraListaResponse From(Compra compra, string? apiBase = null) => new(
        compra.Id,
        compra.Id.ToString("D"),
        compra.Status,
        compra.DataEnvio,
        compra.DataCompra,
        compra.NumeroRecibo,
        compra.Subtotal,
        compra.Descontos,
        compra.Acrescimos,
        compra.Total,
        compra.FormaPagamento,
        compra.FornecedorId,
        compra.Fornecedor?.Nome,
        compra.Fornecedor?.RazaoSocial,
        compra.Fornecedor?.CpfCnpj,
        compra.Fornecedor?.Telefone,
        compra.Fornecedor?.Endereco,
        compra.Paginas,
        compra.TipoDocumento,
        compra.TipoItem,
        compra.DanfeTipo,
        compra.Serie,
        compra.ChaveAcesso,
        compra.ReciboOrigemId,
        ArquivoPublico(compra, apiBase),
        MensagemUsuario.OcultarProvedor(compra.UltimoErro),
        CompraDetalheResponse.ParseJsonPublic(compra.QwenJsonBruto));

    internal static string? ArquivoPublico(Compra compra, string? apiBase)
    {
        if (string.IsNullOrWhiteSpace(apiBase))
        {
            return null;
        }

        var arquivoId = compra.Documentos.OrderByDescending(x => x.DataAtualizacao).FirstOrDefault()?.Id;
        if (arquivoId is null)
        {
            return null;
        }

        return $"{apiBase.TrimEnd('/')}/v1/recibos/{compra.UsuarioId:D}/{compra.ReciboArquivoId:D}/{arquivoId:D}";
    }
}

public sealed record CompraAnexoResponse(Guid Id, int Ordem, string NomeArquivo, string Mime, string Origem, long TamanhoBytes)
{
    public static CompraAnexoResponse From(CompraAnexo anexo) => new(
        anexo.Id, anexo.Ordem, anexo.NomeArquivo, anexo.Mime, anexo.Origem, anexo.TamanhoBytes);
}

public sealed record CompraItemResponse(
    Guid Id,
    int Ordem,
    string DescricaoOriginal,
    string? CodigoImpresso,
    decimal Quantidade,
    string? Unidade,
    decimal? PrecoUnitario,
    decimal? Desconto,
    decimal? Total,
    Guid? ProdutoId,
    string? ProdutoNome,
    string? ProdutoMarca,
    string? ProdutoVariante,
    Guid? ServicoId,
    string? ServicoNome,
    string TipoItem,
    string? NcmSh,
    string? Csosn,
    string? Cfop,
    decimal? ValorLiquido,
    decimal? BaseIcms,
    decimal? ValorIcms,
    decimal? ValorIpi,
    decimal? AliqIcms,
    decimal? AliqIpi)
{
    public static CompraItemResponse From(CompraItem item) => new(
        item.Id,
        item.Ordem,
        item.DescricaoOriginal,
        item.CodigoImpresso,
        item.Quantidade,
        item.Unidade,
        item.PrecoUnitario,
        item.Desconto,
        item.Total,
        item.ProdutoId,
        item.Produto?.Nome,
        item.Produto?.Marca,
        item.Produto?.Variante,
        item.ServicoId,
        item.Servico?.Nome,
        item.TipoItem,
        item.NcmSh,
        item.Csosn,
        item.Cfop,
        item.ValorLiquido,
        item.BaseIcms,
        item.ValorIcms,
        item.ValorIpi,
        item.AliqIcms,
        item.AliqIpi);
}

public sealed record CompraDetalheResponse(
    Guid Id,
    string Codigo,
    string Status,
    DateTimeOffset DataEnvio,
    DateTimeOffset? DataCompra,
    string? NumeroRecibo,
    decimal? Subtotal,
    decimal? Descontos,
    decimal? Acrescimos,
    decimal? Total,
    string? FormaPagamento,
    Guid? FornecedorId,
    string? FornecedorNome,
    string? FornecedorRazaoSocial,
    string? FornecedorCpfCnpj,
    string? FornecedorEndereco,
    string? FornecedorTelefone,
    string? Paginas,
    string TipoDocumento,
    string TipoItem,
    int? DanfeTipo,
    string? Serie,
    string? Folha,
    string? ChaveAcesso,
    string? CodigoBarras,
    string? ProtocoloAutorizacao,
    DateTimeOffset? ProtocoloData,
    string? NaturezaOperacao,
    string? InscricaoEstadual,
    string? InscricaoEstadualSt,
    DateTimeOffset? DataEmissao,
    Guid? ReciboOrigemId,
    string? ArquivoUrl,
    string? HashArquivo,
    JsonElement? QwenJson,
    JsonElement? Divergencias,
    string? UltimoErro,
    IReadOnlyList<CompraAnexoResponse> Anexos,
    IReadOnlyList<CompraItemResponse> Itens,
    IReadOnlyList<CompraListaResponse> Duplicatas,
    CompraNfeDestinatarioResponse? NfeDestinatario,
    CompraNfeImpostoResponse? NfeImposto,
    CompraNfeTransportadorResponse? NfeTransportador,
    CompraNfeAdicionaisResponse? NfeAdicionais)
{
    [JsonPropertyName("geminiJson")]
    public JsonElement? GeminiJson => QwenJson;

    [JsonPropertyName("perplexityJson")]
    public JsonElement? PerplexityJson => QwenJson;

    public static CompraDetalheResponse From(Compra compra, IReadOnlyList<Compra> duplicatas, string? apiBase = null) => new(
        compra.Id,
        compra.Id.ToString("D"),
        compra.Status,
        compra.DataEnvio,
        compra.DataCompra,
        compra.NumeroRecibo,
        compra.Subtotal,
        compra.Descontos,
        compra.Acrescimos,
        compra.Total,
        compra.FormaPagamento,
        compra.FornecedorId,
        compra.Fornecedor?.Nome,
        compra.Fornecedor?.RazaoSocial,
        compra.Fornecedor?.CpfCnpj,
        compra.Fornecedor?.Endereco,
        compra.Fornecedor?.Telefone,
        compra.Paginas,
        compra.TipoDocumento,
        compra.TipoItem,
        compra.DanfeTipo,
        compra.Serie,
        compra.Folha,
        compra.ChaveAcesso,
        compra.CodigoBarras,
        compra.ProtocoloAutorizacao,
        compra.ProtocoloData,
        compra.NaturezaOperacao,
        compra.InscricaoEstadual,
        compra.InscricaoEstadualSt,
        compra.DataEmissao,
        compra.ReciboOrigemId,
        CompraListaResponse.ArquivoPublico(compra, apiBase),
        compra.HashArquivo,
        ParseJson(compra.QwenJsonBruto),
        ParseJson(compra.DivergenciasJson),
        MensagemUsuario.OcultarProvedor(compra.UltimoErro),
        compra.Anexos.OrderBy(x => x.Ordem).Select(CompraAnexoResponse.From).ToList(),
        compra.Itens.OrderBy(x => x.Ordem).Select(CompraItemResponse.From).ToList(),
        duplicatas.Select(d => CompraListaResponse.From(d, apiBase)).ToList(),
        CompraNfeDestinatarioResponse.From(compra.NfeDestinatario),
        CompraNfeImpostoResponse.From(compra.NfeImposto),
        CompraNfeTransportadorResponse.From(compra.NfeTransportador),
        CompraNfeAdicionaisResponse.From(compra.NfeAdicionais));

    public static JsonElement? ParseJsonPublic(string? bruto) => ParseJson(bruto);

    private static JsonElement? ParseJson(string? bruto)
    {
        if (string.IsNullOrWhiteSpace(bruto))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<JsonElement>(bruto);
        }
        catch (JsonException)
        {
            return JsonSerializer.SerializeToElement(new { bruto });
        }
    }
}

public sealed record CompraNfeDestinatarioResponse(
    string? NomeRazaoSocial,
    string? CpfCnpj,
    string? Endereco,
    string? Bairro,
    string? Cep,
    string? Municipio,
    string? Uf,
    string? Telefone,
    string? InscricaoEstadual,
    DateTimeOffset? DataEmissao,
    DateTimeOffset? DataSaida,
    string? HoraSaida)
{
    public static CompraNfeDestinatarioResponse? From(CompraNfeDestinatario? d) =>
        d is null ? null : new(d.NomeRazaoSocial, d.CpfCnpj, d.Endereco, d.Bairro, d.Cep, d.Municipio, d.Uf, d.Telefone, d.InscricaoEstadual, d.DataEmissao, d.DataSaida, d.HoraSaida);
}

public sealed record CompraNfeImpostoResponse(
    decimal? BaseIcms,
    decimal? ValorIcms,
    decimal? BaseIcmsSt,
    decimal? ValorIcmsSt,
    decimal? ValorTotalProdutos,
    decimal? ValorFrete,
    decimal? ValorSeguro,
    decimal? Desconto,
    decimal? OutrasDespesas,
    decimal? ValorIpi,
    decimal? ValorTotalNota)
{
    public static CompraNfeImpostoResponse? From(CompraNfeImposto? i) =>
        i is null ? null : new(i.BaseIcms, i.ValorIcms, i.BaseIcmsSt, i.ValorIcmsSt, i.ValorTotalProdutos, i.ValorFrete, i.ValorSeguro, i.Desconto, i.OutrasDespesas, i.ValorIpi, i.ValorTotalNota);
}

public sealed record CompraNfeTransportadorResponse(
    string? NomeRazaoSocial,
    string? FretePorConta,
    string? CodigoAntt,
    string? Placa,
    string? Uf,
    string? CpfCnpj,
    string? Endereco,
    string? Municipio,
    string? UfEndereco,
    string? InscricaoEstadual,
    decimal? QuantidadeVolumes,
    string? Especie,
    string? Marca,
    string? Numeracao,
    decimal? PesoBruto,
    decimal? PesoLiquido)
{
    public static CompraNfeTransportadorResponse? From(CompraNfeTransportador? t) =>
        t is null ? null : new(t.NomeRazaoSocial, t.FretePorConta, t.CodigoAntt, t.Placa, t.Uf, t.CpfCnpj, t.Endereco, t.Municipio, t.UfEndereco, t.InscricaoEstadual, t.QuantidadeVolumes, t.Especie, t.Marca, t.Numeracao, t.PesoBruto, t.PesoLiquido);
}

public sealed record CompraNfeAdicionaisResponse(
    string? InformacoesComplementares,
    string? ReservadoAoFisco,
    DateTimeOffset? DataHoraImpressao)
{
    public static CompraNfeAdicionaisResponse? From(CompraNfeAdicionais? a) =>
        a is null ? null : new(a.InformacoesComplementares, a.ReservadoAoFisco, a.DataHoraImpressao);
}

public sealed record LimparRascunhosAnterioresResponse(int Removidos);
public sealed record CapturaCriadaResponse(string Url, DateTimeOffset ExpiraEm, string QrPngBase64);
public sealed record CapturaStatusResponse(bool Ativa, bool Consumida, DateTimeOffset? ExpiraEm, int Anexos, bool Concluida);
public sealed record CapturaAbertaResponse(Guid CompraId, string SessionToken, DateTimeOffset ExpiraEm);
public sealed record DocumentoPreviewResponse(string Url);
public sealed record ComprasResumoResponse(
    int TotalCompras,
    int Rascunhos,
    int Processando,
    int EmRevisao,
    int Validadas,
    int Concluidas,
    int Falhas,
    decimal TotalGasto,
    IReadOnlyList<ComprasResumoFornecedorResponse> PorFornecedor,
    int Processados,
    long ArmazenamentoUsadoBytes,
    long ArmazenamentoLimiteBytes,
    long QwenTokensTotal,
    decimal QwenCustoTotalBrl,
    long PerplexityTokensTotal,
    decimal PerplexityCustoTotalBrl,
    int Ano,
    int Mes)
{
    [JsonPropertyName("geminiTokensTotal")]
    public long GeminiTokensTotal => PerplexityTokensTotal > 0 ? PerplexityTokensTotal : QwenTokensTotal;

    [JsonPropertyName("geminiCustoTotalBrl")]
    public decimal GeminiCustoTotalBrl => PerplexityCustoTotalBrl > 0 ? PerplexityCustoTotalBrl : QwenCustoTotalBrl;
}
public sealed record ComprasResumoFornecedorResponse(Guid? FornecedorId, string Nome, int Quantidade, decimal Total);

public sealed record FornecedorResponse(
    Guid Id,
    string Nome,
    string? RazaoSocial,
    string? CpfCnpj,
    string? Telefone,
    string? Endereco,
    int QuantidadeRecibos)
{
    public static FornecedorResponse From(Fornecedor fornecedor, int quantidadeRecibos = 0) => new(
        fornecedor.Id,
        fornecedor.Nome,
        fornecedor.RazaoSocial,
        fornecedor.CpfCnpj,
        fornecedor.Telefone,
        fornecedor.Endereco,
        quantidadeRecibos);
}

public sealed record ProdutoResponse(
    Guid Id,
    string Nome,
    string? Marca,
    string? Variante,
    string UnidadeControle,
    decimal? ConteudoEmbalagem,
    IReadOnlyList<string> Sinonimos,
    Guid? ReciboId,
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
    decimal? AliqIpi)
{
    public static ProdutoResponse From(Produto produto) => new(
        produto.Id,
        produto.Nome,
        produto.Marca,
        produto.Variante,
        produto.UnidadeControle,
        produto.ConteudoEmbalagem,
        produto.Nomes.Select(n => n.Nome).ToList(),
        produto.ReciboId,
        produto.CodigoExterno,
        produto.NcmSh,
        produto.Csosn,
        produto.Cfop,
        produto.ValorUnitario,
        produto.ValorDesconto,
        produto.ValorLiquido,
        produto.BaseIcms,
        produto.ValorIcms,
        produto.ValorIpi,
        produto.AliqIcms,
        produto.AliqIpi);
}

public sealed record ServicoResponse(
    Guid Id,
    string Nome,
    string UnidadeControle,
    Guid? ReciboId,
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
    decimal? AliqIpi)
{
    public static ServicoResponse From(Servico servico) => new(
        servico.Id,
        servico.Nome,
        servico.UnidadeControle,
        servico.ReciboId,
        servico.CodigoExterno,
        servico.NcmSh,
        servico.Csosn,
        servico.Cfop,
        servico.ValorUnitario,
        servico.ValorDesconto,
        servico.ValorLiquido,
        servico.BaseIcms,
        servico.ValorIcms,
        servico.ValorIpi,
        servico.AliqIcms,
        servico.AliqIpi);
}

public sealed record RelatorioItemResponse(
    Guid UsuarioId,
    Guid ReciboId,
    Guid CompraId,
    Guid? ReciboOrigemId,
    Guid? ArquivoId,
    string Codigo,
    DateTimeOffset Data,
    string Status,
    string? NumeroRecibo,
    decimal? Subtotal,
    decimal? Descontos,
    decimal? Acrescimos,
    decimal? TotalCompra,
    string? FormaPagamento,
    Guid? FornecedorId,
    string? FornecedorNome,
    string? FornecedorRazaoSocial,
    string? FornecedorCpfCnpj,
    string? FornecedorTelefone,
    string? FornecedorEndereco,
    Guid? ProdutoId,
    string? ProdutoNome,
    string? ProdutoMarca,
    string? ProdutoVariante,
    decimal? ConteudoEmbalagem,
    string? CodigoProduto,
    string Descricao,
    decimal Quantidade,
    string? Unidade,
    decimal? PrecoUnitario,
    decimal? Desconto,
    decimal? Total,
    string? ArquivoUrl,
    string TipoDocumento,
    string TipoItemCompra,
    int? DanfeTipo,
    string? Serie,
    string? Folha,
    string? ChaveAcesso,
    string? CodigoBarras,
    string? ProtocoloAutorizacao,
    DateTimeOffset? ProtocoloData,
    string? NaturezaOperacao,
    string? InscricaoEstadual,
    string? InscricaoEstadualSt,
    DateTimeOffset? DataEmissao,
    string TipoItem,
    Guid? ServicoId,
    string? ServicoNome,
    string? ItemNcmSh,
    string? ItemCsosn,
    string? ItemCfop,
    decimal? ItemValorLiquido,
    decimal? ItemBaseIcms,
    decimal? ItemValorIcms,
    decimal? ItemValorIpi,
    decimal? ItemAliqIcms,
    decimal? ItemAliqIpi,
    string? ProdutoCodigoExterno,
    string? ProdutoNcmSh,
    string? ProdutoCsosn,
    string? ProdutoCfop,
    decimal? ProdutoValorUnitario,
    decimal? ProdutoValorDesconto,
    decimal? ProdutoValorLiquido,
    decimal? ProdutoBaseIcms,
    decimal? ProdutoValorIcms,
    decimal? ProdutoValorIpi,
    decimal? ProdutoAliqIcms,
    decimal? ProdutoAliqIpi,
    string? ServicoCodigoExterno,
    string? ServicoUnidadeControle,
    string? ServicoNcmSh,
    string? ServicoCsosn,
    string? ServicoCfop,
    decimal? ServicoValorUnitario,
    decimal? ServicoValorDesconto,
    decimal? ServicoValorLiquido,
    decimal? ServicoBaseIcms,
    decimal? ServicoValorIcms,
    decimal? ServicoValorIpi,
    decimal? ServicoAliqIcms,
    decimal? ServicoAliqIpi,
    CompraNfeDestinatarioResponse? NfeDestinatario,
    CompraNfeImpostoResponse? NfeImposto,
    CompraNfeTransportadorResponse? NfeTransportador,
    CompraNfeAdicionaisResponse? NfeAdicionais)
{
    public static RelatorioItemResponse From(AutonomousAudit.Application.Data.RelatorioItemLinha linha, string apiBase)
    {
        string? url = null;
        if (linha.ArquivoId is Guid arquivoId)
        {
            url = $"{apiBase.TrimEnd('/')}/v1/recibos/{linha.UsuarioId:D}/{linha.ReciboId:D}/{arquivoId:D}";
        }

        return new(
            linha.UsuarioId,
            linha.ReciboId,
            linha.CompraId,
            linha.ReciboOrigemId,
            linha.ArquivoId,
            linha.CompraId.ToString("D"),
            linha.Data,
            linha.Status,
            linha.NumeroRecibo,
            linha.Subtotal,
            linha.Descontos,
            linha.Acrescimos,
            linha.TotalCompra,
            linha.FormaPagamento,
            linha.FornecedorId,
            linha.FornecedorNome,
            linha.FornecedorRazaoSocial,
            linha.FornecedorCpfCnpj,
            linha.FornecedorTelefone,
            linha.FornecedorEndereco,
            linha.ProdutoId,
            linha.ProdutoNome,
            linha.ProdutoMarca,
            linha.ProdutoVariante,
            linha.ConteudoEmbalagem,
            linha.Codigo,
            linha.Descricao,
            linha.Quantidade,
            linha.Unidade,
            linha.PrecoUnitario,
            linha.Desconto,
            linha.Total,
            url,
            linha.TipoDocumento,
            linha.TipoItemCompra,
            linha.DanfeTipo,
            linha.Serie,
            linha.Folha,
            linha.ChaveAcesso,
            linha.CodigoBarras,
            linha.ProtocoloAutorizacao,
            linha.ProtocoloData,
            linha.NaturezaOperacao,
            linha.InscricaoEstadual,
            linha.InscricaoEstadualSt,
            linha.DataEmissao,
            linha.TipoItem,
            linha.ServicoId,
            linha.ServicoNome,
            linha.ItemNcmSh,
            linha.ItemCsosn,
            linha.ItemCfop,
            linha.ItemValorLiquido,
            linha.ItemBaseIcms,
            linha.ItemValorIcms,
            linha.ItemValorIpi,
            linha.ItemAliqIcms,
            linha.ItemAliqIpi,
            linha.ProdutoCodigoExterno,
            linha.ProdutoNcmSh,
            linha.ProdutoCsosn,
            linha.ProdutoCfop,
            linha.ProdutoValorUnitario,
            linha.ProdutoValorDesconto,
            linha.ProdutoValorLiquido,
            linha.ProdutoBaseIcms,
            linha.ProdutoValorIcms,
            linha.ProdutoValorIpi,
            linha.ProdutoAliqIcms,
            linha.ProdutoAliqIpi,
            linha.ServicoCodigoExterno,
            linha.ServicoUnidadeControle,
            linha.ServicoNcmSh,
            linha.ServicoCsosn,
            linha.ServicoCfop,
            linha.ServicoValorUnitario,
            linha.ServicoValorDesconto,
            linha.ServicoValorLiquido,
            linha.ServicoBaseIcms,
            linha.ServicoValorIcms,
            linha.ServicoValorIpi,
            linha.ServicoAliqIcms,
            linha.ServicoAliqIpi,
            RelatorioNfe.Destinatario(linha),
            RelatorioNfe.Imposto(linha),
            RelatorioNfe.Transportador(linha),
            RelatorioNfe.Adicionais(linha));
    }
}

public static class RelatorioNfe
{
    public static CompraNfeDestinatarioResponse Destinatario(AutonomousAudit.Application.Data.RelatorioItemLinha linha) =>
        new(linha.DestinatarioNome, linha.DestinatarioCpfCnpj, linha.DestinatarioEndereco, linha.DestinatarioBairro, linha.DestinatarioCep, linha.DestinatarioMunicipio, linha.DestinatarioUf, linha.DestinatarioTelefone, linha.DestinatarioIe, linha.DestinatarioDataEmissao, linha.DestinatarioDataSaida, linha.DestinatarioHoraSaida);

    public static CompraNfeImpostoResponse Imposto(AutonomousAudit.Application.Data.RelatorioItemLinha linha) =>
        new(linha.ImpostoBaseIcms, linha.ImpostoValorIcms, linha.ImpostoBaseIcmsSt, linha.ImpostoValorIcmsSt, linha.ImpostoValorTotalProdutos, linha.ImpostoValorFrete, linha.ImpostoValorSeguro, linha.ImpostoDesconto, linha.ImpostoOutrasDespesas, linha.ImpostoValorIpi, linha.ImpostoValorTotalNota);

    public static CompraNfeTransportadorResponse Transportador(AutonomousAudit.Application.Data.RelatorioItemLinha linha) =>
        new(linha.TransportadorNome, linha.TransportadorFretePorConta, linha.TransportadorCodigoAntt, linha.TransportadorPlaca, linha.TransportadorUf, linha.TransportadorCpfCnpj, linha.TransportadorEndereco, linha.TransportadorMunicipio, linha.TransportadorUfEndereco, linha.TransportadorIe, linha.TransportadorQuantidadeVolumes, linha.TransportadorEspecie, linha.TransportadorMarca, linha.TransportadorNumeracao, linha.TransportadorPesoBruto, linha.TransportadorPesoLiquido);

    public static CompraNfeAdicionaisResponse Adicionais(AutonomousAudit.Application.Data.RelatorioItemLinha linha) =>
        new(linha.AdicionaisInformacoes, linha.AdicionaisReservadoFisco, linha.AdicionaisDataHoraImpressao);
}

public sealed record RelatorioFornecedorBloco(
    Guid? Id,
    string? Nome,
    string? RazaoSocial,
    string? CpfCnpj,
    string? Telefone,
    string? Endereco);

public sealed record RelatorioProdutoBloco(
    Guid? Id,
    string Descricao,
    string? Nome,
    string? Codigo,
    string? Marca,
    string? Variante,
    decimal? ConteudoEmbalagem,
    decimal Quantidade,
    string? Unidade,
    decimal? PrecoUnitario,
    decimal? Desconto,
    decimal? Total,
    string TipoItem,
    Guid? ServicoId,
    string? ServicoNome,
    string? NcmSh,
    string? Csosn,
    string? Cfop,
    decimal? ValorLiquido,
    decimal? BaseIcms,
    decimal? ValorIcms,
    decimal? ValorIpi,
    decimal? AliqIcms,
    decimal? AliqIpi,
    string? CodigoExterno);

public sealed record RelatorioCompraBloco(
    Guid CompraId,
    Guid ReciboId,
    string Codigo,
    DateTimeOffset Data,
    string Status,
    string? NumeroRecibo,
    decimal? Subtotal,
    decimal? Descontos,
    decimal? Acrescimos,
    decimal? Total,
    string? FormaPagamento,
    string? ArquivoUrl,
    string TipoDocumento,
    string TipoItem,
    int? DanfeTipo,
    string? Serie,
    string? Folha,
    string? ChaveAcesso,
    string? CodigoBarras,
    string? ProtocoloAutorizacao,
    DateTimeOffset? ProtocoloData,
    string? NaturezaOperacao,
    string? InscricaoEstadual,
    string? InscricaoEstadualSt,
    DateTimeOffset? DataEmissao,
    RelatorioFornecedorBloco Fornecedor,
    IReadOnlyList<RelatorioProdutoBloco> Produtos,
    IReadOnlyList<RelatorioProdutoBloco> Servicos,
    CompraNfeDestinatarioResponse? NfeDestinatario,
    CompraNfeImpostoResponse? NfeImposto,
    CompraNfeTransportadorResponse? NfeTransportador,
    CompraNfeAdicionaisResponse? NfeAdicionais);

public sealed record RelatorioItensResponse(
    DateTimeOffset Inicio,
    DateTimeOffset Fim,
    Guid? FornecedorId,
    int Limite,
    int Total,
    IReadOnlyList<RelatorioCompraBloco> Compras,
    IReadOnlyList<RelatorioItemResponse> Itens)
{
    public static readonly IReadOnlyList<string> CabecalhoPlanilha =
    [
        "data",
        "compraId",
        "reciboId",
        "numeroRecibo",
        "status",
        "tipoDocumento",
        "tipoItemCompra",
        "danfeTipo",
        "serie",
        "folha",
        "chaveAcesso",
        "codigoBarras",
        "protocoloAutorizacao",
        "protocoloData",
        "naturezaOperacao",
        "inscricaoEstadual",
        "inscricaoEstadualSt",
        "dataEmissao",
        "subtotal",
        "descontos",
        "acrescimos",
        "totalCompra",
        "formaPagamento",
        "fornecedorPrestador",
        "razaoSocial",
        "cpfCnpj",
        "telefone",
        "endereco",
        "tipoItem",
        "produto",
        "codigo",
        "marca",
        "variante",
        "conteudoEmbalagem",
        "produtoCodigoExterno",
        "produtoNcmSh",
        "produtoCsosn",
        "produtoCfop",
        "produtoValorUnitario",
        "produtoValorDesconto",
        "produtoValorLiquido",
        "produtoBaseIcms",
        "produtoValorIcms",
        "produtoValorIpi",
        "produtoAliqIcms",
        "produtoAliqIpi",
        "servicoId",
        "servicoNome",
        "servicoCodigoExterno",
        "servicoUnidade",
        "servicoNcmSh",
        "servicoCsosn",
        "servicoCfop",
        "servicoValorUnitario",
        "servicoValorDesconto",
        "servicoValorLiquido",
        "servicoBaseIcms",
        "servicoValorIcms",
        "servicoValorIpi",
        "servicoAliqIcms",
        "servicoAliqIpi",
        "quantidade",
        "unidade",
        "precoUnitario",
        "desconto",
        "valorLiquidoItem",
        "ncmShItem",
        "csosnItem",
        "cfopItem",
        "baseIcmsItem",
        "valorIcmsItem",
        "valorIpiItem",
        "aliqIcmsItem",
        "aliqIpiItem",
        "totalProduto",
        "tomadorDestinatarioNome",
        "tomadorDestinatarioCpfCnpj",
        "tomadorDestinatarioEndereco",
        "tomadorDestinatarioBairro",
        "tomadorDestinatarioCep",
        "tomadorDestinatarioMunicipio",
        "tomadorDestinatarioUf",
        "tomadorDestinatarioTelefone",
        "tomadorDestinatarioIe",
        "tomadorDestinatarioDataEmissao",
        "tomadorDestinatarioDataSaida",
        "tomadorDestinatarioHoraSaida",
        "impostoBaseIcms",
        "impostoValorIcms",
        "impostoBaseIcmsSt",
        "impostoValorIcmsSt",
        "impostoValorTotalProdutos",
        "impostoValorFrete",
        "impostoValorSeguro",
        "impostoDesconto",
        "impostoOutrasDespesas",
        "impostoValorIpi",
        "impostoValorTotalNota",
        "transportadorNome",
        "transportadorFretePorConta",
        "transportadorCodigoAntt",
        "transportadorPlaca",
        "transportadorUf",
        "transportadorCpfCnpj",
        "transportadorEndereco",
        "transportadorMunicipio",
        "transportadorUfEndereco",
        "transportadorIe",
        "transportadorQuantidadeVolumes",
        "transportadorEspecie",
        "transportadorMarca",
        "transportadorNumeracao",
        "transportadorPesoBruto",
        "transportadorPesoLiquido",
        "adicionaisInformacoes",
        "adicionaisReservadoFisco",
        "adicionaisDataHoraImpressao",
        "arquivoUrl"
    ];

    public static RelatorioItensResponse From(
        RelatorioItensResult resultado,
        DateTimeOffset inicio,
        DateTimeOffset fim,
        Guid? fornecedorId,
        string apiBase)
    {
        var itens = resultado.Itens.Select(x => RelatorioItemResponse.From(x, apiBase)).ToList();
        var compras = itens
            .GroupBy(x => x.CompraId)
            .Select(g =>
            {
                var primeiro = g.First();
                var blocos = g.Select(ProdutoDeItem).ToList();
                return new RelatorioCompraBloco(
                    primeiro.CompraId,
                    primeiro.ReciboId,
                    primeiro.Codigo,
                    primeiro.Data,
                    primeiro.Status,
                    primeiro.NumeroRecibo,
                    primeiro.Subtotal,
                    primeiro.Descontos,
                    primeiro.Acrescimos,
                    primeiro.TotalCompra,
                    primeiro.FormaPagamento,
                    primeiro.ArquivoUrl,
                    primeiro.TipoDocumento,
                    primeiro.TipoItemCompra,
                    primeiro.DanfeTipo,
                    primeiro.Serie,
                    primeiro.Folha,
                    primeiro.ChaveAcesso,
                    primeiro.CodigoBarras,
                    primeiro.ProtocoloAutorizacao,
                    primeiro.ProtocoloData,
                    primeiro.NaturezaOperacao,
                    primeiro.InscricaoEstadual,
                    primeiro.InscricaoEstadualSt,
                    primeiro.DataEmissao,
                    new RelatorioFornecedorBloco(
                        primeiro.FornecedorId,
                        primeiro.FornecedorNome,
                        primeiro.FornecedorRazaoSocial,
                        primeiro.FornecedorCpfCnpj,
                        primeiro.FornecedorTelefone,
                        primeiro.FornecedorEndereco),
                    blocos,
                    blocos.Where(x => string.Equals(x.TipoItem, "servico", StringComparison.OrdinalIgnoreCase)).ToList(),
                    primeiro.NfeDestinatario,
                    primeiro.NfeImposto,
                    primeiro.NfeTransportador,
                    primeiro.NfeAdicionais);
            })
            .OrderByDescending(x => x.Data)
            .ToList();

        return new(inicio, fim, fornecedorId, resultado.Limite, compras.Count, compras, itens);
    }

    private static RelatorioProdutoBloco ProdutoDeItem(RelatorioItemResponse p) =>
        new(
            p.ProdutoId,
            p.Descricao,
            p.ProdutoNome ?? p.ServicoNome,
            p.CodigoProduto,
            p.ProdutoMarca,
            p.ProdutoVariante,
            p.ConteudoEmbalagem,
            p.Quantidade,
            p.Unidade,
            p.PrecoUnitario,
            p.Desconto,
            p.Total,
            p.TipoItem,
            p.ServicoId,
            p.ServicoNome,
            p.ItemNcmSh,
            p.ItemCsosn,
            p.ItemCfop,
            p.ItemValorLiquido,
            p.ItemBaseIcms,
            p.ItemValorIcms,
            p.ItemValorIpi,
            p.ItemAliqIcms,
            p.ItemAliqIpi,
            p.ProdutoCodigoExterno ?? p.ServicoCodigoExterno);

    public IReadOnlyList<IReadOnlyList<string>> LinhasPlanilha()
    {
        return Itens.Select(item => (IReadOnlyList<string>)
        [
            DataBr(item.Data),
            item.CompraId.ToString("D"),
            item.ReciboId.ToString("D"),
            item.NumeroRecibo ?? "",
            item.Status,
            item.TipoDocumento,
            item.TipoItemCompra,
            item.DanfeTipo?.ToString() ?? "",
            item.Serie ?? "",
            item.Folha ?? "",
            item.ChaveAcesso ?? "",
            item.CodigoBarras ?? "",
            item.ProtocoloAutorizacao ?? "",
            DataBr(item.ProtocoloData),
            item.NaturezaOperacao ?? "",
            item.InscricaoEstadual ?? "",
            item.InscricaoEstadualSt ?? "",
            DataBr(item.DataEmissao),
            Numero(item.Subtotal),
            Numero(item.Descontos),
            Numero(item.Acrescimos),
            Numero(item.TotalCompra),
            item.FormaPagamento ?? "",
            item.FornecedorNome ?? "",
            item.FornecedorRazaoSocial ?? "",
            item.FornecedorCpfCnpj ?? "",
            item.FornecedorTelefone ?? "",
            item.FornecedorEndereco ?? "",
            item.TipoItem,
            item.ProdutoNome ?? item.Descricao,
            item.CodigoProduto ?? "",
            item.ProdutoMarca ?? "",
            item.ProdutoVariante ?? "",
            Numero(item.ConteudoEmbalagem),
            item.ProdutoCodigoExterno ?? "",
            item.ProdutoNcmSh ?? "",
            item.ProdutoCsosn ?? "",
            item.ProdutoCfop ?? "",
            Numero(item.ProdutoValorUnitario),
            Numero(item.ProdutoValorDesconto),
            Numero(item.ProdutoValorLiquido),
            Numero(item.ProdutoBaseIcms),
            Numero(item.ProdutoValorIcms),
            Numero(item.ProdutoValorIpi),
            Numero(item.ProdutoAliqIcms),
            Numero(item.ProdutoAliqIpi),
            item.ServicoId?.ToString("D") ?? "",
            item.ServicoNome ?? "",
            item.ServicoCodigoExterno ?? "",
            item.ServicoUnidadeControle ?? "",
            item.ServicoNcmSh ?? "",
            item.ServicoCsosn ?? "",
            item.ServicoCfop ?? "",
            Numero(item.ServicoValorUnitario),
            Numero(item.ServicoValorDesconto),
            Numero(item.ServicoValorLiquido),
            Numero(item.ServicoBaseIcms),
            Numero(item.ServicoValorIcms),
            Numero(item.ServicoValorIpi),
            Numero(item.ServicoAliqIcms),
            Numero(item.ServicoAliqIpi),
            Numero(item.Quantidade),
            item.Unidade ?? "",
            Numero(item.PrecoUnitario),
            Numero(item.Desconto),
            Numero(item.ItemValorLiquido),
            item.ItemNcmSh ?? "",
            item.ItemCsosn ?? "",
            item.ItemCfop ?? "",
            Numero(item.ItemBaseIcms),
            Numero(item.ItemValorIcms),
            Numero(item.ItemValorIpi),
            Numero(item.ItemAliqIcms),
            Numero(item.ItemAliqIpi),
            Numero(item.Total),
            item.NfeDestinatario?.NomeRazaoSocial ?? "",
            item.NfeDestinatario?.CpfCnpj ?? "",
            item.NfeDestinatario?.Endereco ?? "",
            item.NfeDestinatario?.Bairro ?? "",
            item.NfeDestinatario?.Cep ?? "",
            item.NfeDestinatario?.Municipio ?? "",
            item.NfeDestinatario?.Uf ?? "",
            item.NfeDestinatario?.Telefone ?? "",
            item.NfeDestinatario?.InscricaoEstadual ?? "",
            DataBr(item.NfeDestinatario?.DataEmissao),
            DataBr(item.NfeDestinatario?.DataSaida),
            item.NfeDestinatario?.HoraSaida ?? "",
            Numero(item.NfeImposto?.BaseIcms),
            Numero(item.NfeImposto?.ValorIcms),
            Numero(item.NfeImposto?.BaseIcmsSt),
            Numero(item.NfeImposto?.ValorIcmsSt),
            Numero(item.NfeImposto?.ValorTotalProdutos),
            Numero(item.NfeImposto?.ValorFrete),
            Numero(item.NfeImposto?.ValorSeguro),
            Numero(item.NfeImposto?.Desconto),
            Numero(item.NfeImposto?.OutrasDespesas),
            Numero(item.NfeImposto?.ValorIpi),
            Numero(item.NfeImposto?.ValorTotalNota),
            item.NfeTransportador?.NomeRazaoSocial ?? "",
            item.NfeTransportador?.FretePorConta ?? "",
            item.NfeTransportador?.CodigoAntt ?? "",
            item.NfeTransportador?.Placa ?? "",
            item.NfeTransportador?.Uf ?? "",
            item.NfeTransportador?.CpfCnpj ?? "",
            item.NfeTransportador?.Endereco ?? "",
            item.NfeTransportador?.Municipio ?? "",
            item.NfeTransportador?.UfEndereco ?? "",
            item.NfeTransportador?.InscricaoEstadual ?? "",
            Numero(item.NfeTransportador?.QuantidadeVolumes),
            item.NfeTransportador?.Especie ?? "",
            item.NfeTransportador?.Marca ?? "",
            item.NfeTransportador?.Numeracao ?? "",
            Numero(item.NfeTransportador?.PesoBruto),
            Numero(item.NfeTransportador?.PesoLiquido),
            item.NfeAdicionais?.InformacoesComplementares ?? "",
            item.NfeAdicionais?.ReservadoAoFisco ?? "",
            DataBr(item.NfeAdicionais?.DataHoraImpressao),
            item.ArquivoUrl ?? ""
        ]).ToList();
    }

    private static string Numero(decimal? valor) =>
        valor is null ? "" : valor.Value.ToString("0.####", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"));

    private static string DataBr(DateTimeOffset? valor) =>
        valor is null ? "" : valor.Value.ToOffset(TimeSpan.FromHours(-3)).ToString("dd/MM/yyyy HH:mm");
}
