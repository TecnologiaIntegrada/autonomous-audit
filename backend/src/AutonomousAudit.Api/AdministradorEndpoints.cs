using System.Net;
using AutonomousAudit.Application.RequestHandlers;
using AutonomousAudit.Domain;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AutonomousAudit.Api;

public static class AdministradorEndpoints
{
    public static WebApplication MapAdministradores(this WebApplication app)
    {
        var group = app.MapGroup("/v1/administradores")
            .WithTags("Administradores")
            .RequireAdministradorSistema();

        group.MapGet("", Listar)
            .Produces<IReadOnlyList<AdministradorSistemaResponse>>()
            .WithSummary("Lista os administradores do sistema (ADM_SYS)")
            .WithDescription(SwaggerDocs.Bloco(
                "Mostra quem pode operar cadastros de outros usuarios nas rotas /v1/usuarios.",
                "Governanca: conferir quem esta na tabela adm_sys.",
                "- JWT de um usuario cujo ID esteja em ADM_SYS. Nao usa recurso de catalogo.",
                "Le id_admin, id_usuario, e-mail e nome.",
                "- **200** lista.\n- **403** chamador fora de ADM_SYS."));

        group.MapPost("", Incluir)
            .Accepts<IncluirAdministradorBody>("application/json")
            .Produces<AdministradorSistemaResponse>((int)HttpStatusCode.Created)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.BadRequest)
            .WithSummary("Inclui um usuario em ADM_SYS")
            .WithDescription(SwaggerDocs.Bloco(
                "Passa a permitir que aquele usuario manipule qualquer cadastro em /v1/usuarios (ainda precisa dos recursos do catalogo).",
                "Promover um operador a administrador do sistema.",
                "- JWT de ADM_SYS.\n- JSON `{ \"usuarioId\": \"guid\" }`.",
                "Grava id_admin e id_usuario. Um usuario so pode aparecer uma vez.",
                "- **201** incluido.\n- **400** ja estava em ADM_SYS.\n- **404** usuario inexistente."));

        group.MapDelete("/{id:guid}", Remover)
            .Produces((int)HttpStatusCode.NoContent)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.BadRequest)
            .WithSummary("Remove um usuario de ADM_SYS")
            .WithDescription(SwaggerDocs.Bloco(
                "Revoga o direito de operar outros usuarios. O cadastro da pessoa permanece.",
                "Quando a pessoa deixa de ser administrador do sistema.",
                "- JWT de ADM_SYS.\n- `{id}` e o `id_admin`, nao o id do usuario.",
                "Apaga a linha em adm_sys. Impede remover o ultimo administrador.",
                "- **204** removido.\n- **400** ultimo administrador.\n- **404** id_admin inexistente."));

        return app;
    }

    private static async Task<IResult> Listar(IMediator mediator, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new ListarAdministradoresQuery(), cancellationToken);
        return result switch
        {
            ListarAdministradoresOk ok => Results.Ok(ok.Administradores.Select(AdministradorSistemaResponse.From).ToList()),
            ListarAdministradoresFail failed => EndpointValidation.Problem(
                "Falha ao listar administradores",
                failed.Message,
                StatusCodes.Status502BadGateway),
            _ => throw new ArgumentOutOfRangeException(nameof(result))
        };
    }

    private static async Task<IResult> Incluir(
        [FromBody] IncluirAdministradorBody body,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new IncluirAdministradorCommand(body.UsuarioId), cancellationToken);
        return result switch
        {
            IncluirAdministradorCreated created => Results.Created(
                $"/v1/administradores/{created.Administrador.Id}",
                AdministradorSistemaResponse.From(created.Administrador)),
            IncluirAdministradorNotFound notFound => EndpointValidation.Problem(
                "Nao encontrado",
                notFound.Message,
                StatusCodes.Status404NotFound),
            IncluirAdministradorBadRequest badRequest => EndpointValidation.Problem(
                "Requisicao invalida",
                badRequest.Message,
                StatusCodes.Status400BadRequest),
            IncluirAdministradorFail failed => EndpointValidation.Problem(
                "Falha ao incluir administrador",
                failed.Message,
                StatusCodes.Status502BadGateway),
            _ => throw new ArgumentOutOfRangeException(nameof(result))
        };
    }

    private static async Task<IResult> Remover(Guid id, IMediator mediator, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new RemoverAdministradorCommand(id), cancellationToken);
        return result switch
        {
            RemoverAdministradorOk => Results.NoContent(),
            RemoverAdministradorNotFound notFound => EndpointValidation.Problem(
                "Nao encontrado",
                notFound.Message,
                StatusCodes.Status404NotFound),
            RemoverAdministradorBadRequest badRequest => EndpointValidation.Problem(
                "Requisicao invalida",
                badRequest.Message,
                StatusCodes.Status400BadRequest),
            RemoverAdministradorFail failed => EndpointValidation.Problem(
                "Falha ao remover administrador",
                failed.Message,
                StatusCodes.Status502BadGateway),
            _ => throw new ArgumentOutOfRangeException(nameof(result))
        };
    }
}

public sealed record IncluirAdministradorBody(Guid UsuarioId);

public sealed record AdministradorSistemaResponse(
    Guid IdAdmin,
    Guid IdUsuario,
    string? Nome,
    string? EmailPrincipal,
    DateTimeOffset DataCriacao)
{
    public static AdministradorSistemaResponse From(AdministradorSistema administrador) =>
        new(
            administrador.Id,
            administrador.UsuarioId,
            administrador.Usuario?.Nome,
            administrador.Usuario?.EmailPrincipal,
            administrador.DataCriacao);
}
