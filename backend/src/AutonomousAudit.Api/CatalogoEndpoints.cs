using System.Net;
using AutonomousAudit.Application.RequestHandlers;
using AutonomousAudit.Application.Security;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AutonomousAudit.Api;

public static class CatalogoEndpoints
{
    public static WebApplication MapCatalogo(this WebApplication app)
    {
        var modulos = app.MapGroup("/v1/modulos").WithTags("Modulos");

        modulos.MapGet("", ListarModulos)
            .RequireRecurso(RecursoChaves.ModuloRead)
            .Produces<IReadOnlyList<ModuloResponse>>()
            .WithSummary("Lista os módulos e os recursos de cada um")
            .WithDescription(SwaggerDocs.Bloco(
                "Mapa do catálogo de autorização: módulos (e-mail, arquivos, Google...) com as ações filhas.",
                "Entender o que existe para montar perfis, ou alimentar a tela de permissões.",
                "- JWT + recurso `modulo.read`.",
                "Devolve código, nome, descrição e a lista de recursos (chave, método HTTP e rota típica).",
                "- **200** catálogo completo."));

        modulos.MapGet("/{id:guid}", ObterModulo)
            .RequireRecurso(RecursoChaves.ModuloRead)
            .Produces<ModuloResponse>()
            .Produces((int)HttpStatusCode.NotFound)
            .WithSummary("Obtém um módulo e seus recursos")
            .WithDescription(SwaggerDocs.Bloco(
                "Detalhe de um agrupador do catálogo.",
                "Quando se vai editar o módulo ou conferir quais ações ele concentra.",
                "- JWT + recurso `modulo.read`.",
                "Retorna o módulo e os recursos vinculados.",
                "- **200** módulo.\n- **404** id inexistente."));

        modulos.MapPost("", CriarModulo)
            .RequireRecurso(RecursoChaves.ModuloCreate)
            .Accepts<ModuloBody>("application/json")
            .Produces<ModuloResponse>((int)HttpStatusCode.Created)
            .Produces((int)HttpStatusCode.BadRequest)
            .WithSummary("Cadastra um módulo no catálogo")
            .WithDescription(SwaggerDocs.Bloco(
                "Cria um novo agrupador de permissões (ex.: um produto interno futuro).",
                "Governança: expandir o modelo de autorização sem deploy de código de negócio.",
                "- JWT + recurso `modulo.create`.\n" +
                "- JSON `{ \"codigo\", \"nome\", \"descricao\" }`. Código estável, único.",
                "Inclui o módulo vazio; os recursos são criados em POST /v1/recursos.",
                "- **201** módulo criado.\n- **400** código duplicado ou inválido."));

        modulos.MapPut("/{id:guid}", AtualizarModulo)
            .RequireRecurso(RecursoChaves.ModuloUpdate)
            .Accepts<ModuloBody>("application/json")
            .Produces<ModuloResponse>()
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.BadRequest)
            .WithSummary("Atualiza nome e descrição de um módulo")
            .WithDescription(SwaggerDocs.Bloco(
                "Ajusta os dados cadastrais do agrupador. Mudar o código com cuidado: ele identifica o módulo nas permissões.",
                "Correção de rótulo para a tela de administração.",
                "- JWT + recurso `modulo.update`.",
                "Atualiza código, nome e descrição se válidos e únicos.",
                "- **200** módulo atualizado.\n- **404** inexistente."));

        modulos.MapDelete("/{id:guid}", ExcluirModulo)
            .RequireRecurso(RecursoChaves.ModuloDelete)
            .Produces((int)HttpStatusCode.NoContent)
            .Produces((int)HttpStatusCode.NotFound)
            .WithSummary("Exclui um módulo e os recursos vinculados")
            .WithDescription(SwaggerDocs.Bloco(
                "Remove o agrupador, as ações filhas e as permissões de usuários ligadas a esses recursos.",
                "Limpeza de catálogo. Impacta quem já tinha aquelas permissões.",
                "- JWT + recurso `modulo.delete`.",
                "Apaga módulo, recursos do módulo e vínculos em `usuario_permissoes`.",
                "- **204** excluído.\n- **404** inexistente.",
                atencao: "Ação em cascata. Confirme que nenhum fluxo de negócio ainda depende desses recursos."));

        var recursos = app.MapGroup("/v1/recursos").WithTags("Recursos");

        recursos.MapGet("", ListarRecursos)
            .RequireRecurso(RecursoChaves.RecursoRead)
            .Produces<IReadOnlyList<RecursoCatalogoResponse>>()
            .WithSummary("Lista os recursos (ações) do catálogo")
            .WithDescription(SwaggerDocs.Bloco(
                "Lista plana do que pode ser autorizado: cada item é uma ação da API (`email.send`, `arquivo.create`, ...).",
                "Montar a tela de permissões ou filtrar por módulo.",
                "- JWT + recurso `recurso.read`.\n" +
                "- Query opcional `moduloId` para ver só as ações daquele módulo.",
                "Devolve id, código, chave, nome, método HTTP e rota de referência.",
                "- **200** lista de recursos."));

        recursos.MapGet("/{id:guid}", ObterRecurso)
            .RequireRecurso(RecursoChaves.RecursoRead)
            .Produces<RecursoCatalogoResponse>()
            .Produces((int)HttpStatusCode.NotFound)
            .WithSummary("Obtém um recurso do catálogo")
            .WithDescription(SwaggerDocs.Bloco(
                "Ficha de uma ação autorizável.",
                "Antes de atribuir ou revogar pelo GUID em DELETE /usuarios/{id}/permissoes/{recursoId}.",
                "- JWT + recurso `recurso.read`.",
                "Retorna chave, nome, método e rota.",
                "- **200** recurso.\n- **404** inexistente."));

        recursos.MapPost("", CriarRecurso)
            .RequireRecurso(RecursoChaves.RecursoCreate)
            .Accepts<RecursoBody>("application/json")
            .Produces<RecursoCatalogoResponse>((int)HttpStatusCode.Created)
            .Produces((int)HttpStatusCode.BadRequest)
            .WithSummary("Cadastra um recurso em um módulo")
            .WithDescription(SwaggerDocs.Bloco(
                "Cria uma nova ação que poderá ser ligada a usuários. Sem este cadastro, a rota correspondente continuaria 403 para quem não tiver a chave.",
                "Quando uma nova capacidade da API precisa entrar no modelo de autorização.",
                "- JWT + recurso `recurso.create`.\n" +
                "- JSON: `moduloId`, `codigo`, `nome`, `metodoHttp`, `rota` e `chave` opcional (padrão modulo.codigo).",
                "Inclui o recurso no módulo informado. A chave deve ser única.",
                "- **201** recurso criado.\n- **400** módulo inexistente ou chave duplicada."));

        recursos.MapPut("/{id:guid}", AtualizarRecurso)
            .RequireRecurso(RecursoChaves.RecursoUpdate)
            .Accepts<RecursoBody>("application/json")
            .Produces<RecursoCatalogoResponse>()
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.BadRequest)
            .WithSummary("Atualiza um recurso do catálogo")
            .WithDescription(SwaggerDocs.Bloco(
                "Altera rótulo, rota de referência ou módulo pai de uma ação já existente.",
                "Manutenção do catálogo alinhada às rotas reais da API.",
                "- JWT + recurso `recurso.update`.",
                "Atualiza os campos cadastrais. Mudar a `chave` afeta o que os usuários precisam ter no perfil.",
                "- **200** recurso atualizado.\n- **404** inexistente."));

        recursos.MapDelete("/{id:guid}", ExcluirRecurso)
            .RequireRecurso(RecursoChaves.RecursoDelete)
            .Produces((int)HttpStatusCode.NoContent)
            .Produces((int)HttpStatusCode.NotFound)
            .WithSummary("Exclui um recurso do catálogo")
            .WithDescription(SwaggerDocs.Bloco(
                "Remove a ação e as permissões de usuários que a possuíam.",
                "Desligar uma capacidade que não existe mais na API.",
                "- JWT + recurso `recurso.delete`.",
                "Apaga o recurso e os vínculos em `usuario_permissoes`.",
                "- **204** excluído.\n- **404** inexistente."));

        return app;
    }

    private static async Task<IResult> ListarModulos(IMediator mediator, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new ListarModulosQuery(), cancellationToken);
        return result switch
        {
            ListarModulosOk ok => Results.Ok(ok.Modulos.Select(ModuloResponse.From).ToList()),
            ListarModulosFail failed => EndpointValidation.Problem("Falha ao listar modulos", failed.Message, StatusCodes.Status502BadGateway),
            _ => throw new ArgumentOutOfRangeException(nameof(result))
        };
    }

    private static async Task<IResult> ObterModulo(Guid id, IMediator mediator, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new ObterModuloQuery(id), cancellationToken);
        return result switch
        {
            ObterModuloOk ok => Results.Ok(ModuloResponse.From(ok.Modulo)),
            ObterModuloNotFound notFound => EndpointValidation.Problem("Nao encontrado", notFound.Message, StatusCodes.Status404NotFound),
            ObterModuloFail failed => EndpointValidation.Problem("Falha ao obter modulo", failed.Message, StatusCodes.Status502BadGateway),
            _ => throw new ArgumentOutOfRangeException(nameof(result))
        };
    }

    private static async Task<IResult> CriarModulo(
        [FromBody] ModuloBody body,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new CriarModuloCommand(body.Codigo ?? string.Empty, body.Nome ?? string.Empty, body.Descricao ?? string.Empty),
            cancellationToken);
        return result switch
        {
            AlterarModuloCreated created => Results.Created($"/v1/modulos/{created.Modulo.Id}", ModuloResponse.From(created.Modulo)),
            AlterarModuloBadRequest badRequest => EndpointValidation.Problem("Requisicao invalida", badRequest.Message, StatusCodes.Status400BadRequest),
            AlterarModuloFail failed => EndpointValidation.Problem("Falha ao criar modulo", failed.Message, StatusCodes.Status502BadGateway),
            _ => throw new ArgumentOutOfRangeException(nameof(result))
        };
    }

    private static async Task<IResult> AtualizarModulo(
        Guid id,
        [FromBody] ModuloBody body,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new AtualizarModuloCommand(id, body.Codigo ?? string.Empty, body.Nome ?? string.Empty, body.Descricao ?? string.Empty),
            cancellationToken);
        return result switch
        {
            AlterarModuloOk ok => Results.Ok(ModuloResponse.From(ok.Modulo)),
            AlterarModuloNotFound notFound => EndpointValidation.Problem("Nao encontrado", notFound.Message, StatusCodes.Status404NotFound),
            AlterarModuloBadRequest badRequest => EndpointValidation.Problem("Requisicao invalida", badRequest.Message, StatusCodes.Status400BadRequest),
            AlterarModuloFail failed => EndpointValidation.Problem("Falha ao atualizar modulo", failed.Message, StatusCodes.Status502BadGateway),
            _ => throw new ArgumentOutOfRangeException(nameof(result))
        };
    }

    private static async Task<IResult> ExcluirModulo(Guid id, IMediator mediator, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new ExcluirModuloCommand(id), cancellationToken);
        return MapExclusao(result);
    }

    private static async Task<IResult> ListarRecursos(Guid? moduloId, IMediator mediator, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new ListarRecursosQuery(moduloId), cancellationToken);
        return result switch
        {
            ListarRecursosOk ok => Results.Ok(ok.Recursos.Select(RecursoCatalogoResponse.From).ToList()),
            ListarRecursosFail failed => EndpointValidation.Problem("Falha ao listar recursos", failed.Message, StatusCodes.Status502BadGateway),
            _ => throw new ArgumentOutOfRangeException(nameof(result))
        };
    }

    private static async Task<IResult> ObterRecurso(Guid id, IMediator mediator, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new ObterRecursoQuery(id), cancellationToken);
        return result switch
        {
            ObterRecursoOk ok => Results.Ok(RecursoCatalogoResponse.From(ok.Recurso)),
            ObterRecursoNotFound notFound => EndpointValidation.Problem("Nao encontrado", notFound.Message, StatusCodes.Status404NotFound),
            ObterRecursoFail failed => EndpointValidation.Problem("Falha ao obter recurso", failed.Message, StatusCodes.Status502BadGateway),
            _ => throw new ArgumentOutOfRangeException(nameof(result))
        };
    }

    private static async Task<IResult> CriarRecurso(
        [FromBody] RecursoBody body,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new CriarRecursoCommand(
                body.ModuloId,
                body.Codigo ?? string.Empty,
                body.Nome ?? string.Empty,
                body.MetodoHttp,
                body.Rota,
                body.Chave),
            cancellationToken);
        return result switch
        {
            AlterarRecursoCreated created => Results.Created($"/v1/recursos/{created.Recurso.Id}", RecursoCatalogoResponse.From(created.Recurso)),
            AlterarRecursoBadRequest badRequest => EndpointValidation.Problem("Requisicao invalida", badRequest.Message, StatusCodes.Status400BadRequest),
            AlterarRecursoFail failed => EndpointValidation.Problem("Falha ao criar recurso", failed.Message, StatusCodes.Status502BadGateway),
            _ => throw new ArgumentOutOfRangeException(nameof(result))
        };
    }

    private static async Task<IResult> AtualizarRecurso(
        Guid id,
        [FromBody] RecursoBody body,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new AtualizarRecursoCommand(
                id,
                body.ModuloId,
                body.Codigo ?? string.Empty,
                body.Nome ?? string.Empty,
                body.MetodoHttp,
                body.Rota,
                body.Chave),
            cancellationToken);
        return result switch
        {
            AlterarRecursoOk ok => Results.Ok(RecursoCatalogoResponse.From(ok.Recurso)),
            AlterarRecursoNotFound notFound => EndpointValidation.Problem("Nao encontrado", notFound.Message, StatusCodes.Status404NotFound),
            AlterarRecursoBadRequest badRequest => EndpointValidation.Problem("Requisicao invalida", badRequest.Message, StatusCodes.Status400BadRequest),
            AlterarRecursoFail failed => EndpointValidation.Problem("Falha ao atualizar recurso", failed.Message, StatusCodes.Status502BadGateway),
            _ => throw new ArgumentOutOfRangeException(nameof(result))
        };
    }

    private static async Task<IResult> ExcluirRecurso(Guid id, IMediator mediator, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new ExcluirRecursoCommand(id), cancellationToken);
        return MapExclusao(result);
    }

    private static IResult MapExclusao(ExcluirCatalogoResult result) =>
        result switch
        {
            ExcluirCatalogoOk => Results.NoContent(),
            ExcluirCatalogoNotFound notFound => EndpointValidation.Problem("Nao encontrado", notFound.Message, StatusCodes.Status404NotFound),
            ExcluirCatalogoFail failed => EndpointValidation.Problem("Falha ao excluir", failed.Message, StatusCodes.Status502BadGateway),
            _ => throw new ArgumentOutOfRangeException(nameof(result))
        };
}

public sealed record ModuloBody(string? Codigo, string? Nome, string? Descricao);

public sealed record RecursoBody(
    Guid ModuloId,
    string? Codigo,
    string? Nome,
    string? MetodoHttp,
    string? Rota,
    string? Chave);
