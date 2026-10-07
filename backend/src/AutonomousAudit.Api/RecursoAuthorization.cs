using AutonomousAudit.Application;
using AutonomousAudit.Application.Data;
using Microsoft.Extensions.Logging;

namespace AutonomousAudit.Api;

public static class RecursoAuthorization
{
    public const string HttpItemUsuarioId = "UsuarioAutenticadoId";
    public const string HttpItemUsuarioNome = "UsuarioAutenticadoNome";
    public const string HttpItemUsuarioEmail = "UsuarioAutenticadoEmail";
    public const string HttpItemUsuarioJti = "UsuarioAutenticadoJti";

    public static RouteHandlerBuilder RequireRecurso(this RouteHandlerBuilder builder, string chaveRecurso)
    {
        return builder
            .AddEndpointFilter(new RecursoEndpointFilter(chaveRecurso))
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);
    }

    public static RouteHandlerBuilder RestrictUsuarioAoProprioOuAdmin(this RouteHandlerBuilder builder)
    {
        return builder
            .AddEndpointFilter(new UsuarioEscopoFilter())
            .Produces(StatusCodes.Status403Forbidden);
    }

    public static RouteHandlerBuilder RequireAdministradorSistema(this RouteHandlerBuilder builder)
    {
        return builder
            .AddEndpointFilter(new AdministradorSistemaFilter())
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);
    }

    public static RouteGroupBuilder RequireAdministradorSistema(this RouteGroupBuilder builder)
    {
        return builder.AddEndpointFilter(new AdministradorSistemaFilter());
    }
}

public sealed class RecursoEndpointFilter : IEndpointFilter
{
    private readonly string _chaveRecurso;

    public RecursoEndpointFilter(string chaveRecurso)
    {
        _chaveRecurso = chaveRecurso;
    }

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var verificador = context.HttpContext.RequestServices.GetRequiredService<IVerificadorAcesso>();
        var resultado = await verificador.VerificarAsync(
            context.HttpContext.Request.Headers.Authorization.ToString(),
            _chaveRecurso,
            context.HttpContext.RequestAborted);

        if (resultado is AcessoNegado negado)
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<RecursoEndpointFilter>>();
            logger.LogWarning(
                "HTTP {Status} {Metodo} {Path} recurso={Recurso} titulo={Titulo} detalhe={Detalhe} trace={TraceId}",
                negado.Status,
                context.HttpContext.Request.Method,
                context.HttpContext.Request.Path.Value,
                _chaveRecurso,
                negado.Titulo,
                negado.Detalhe,
                context.HttpContext.TraceIdentifier);
            SeqErrorLog.Capturar(
                context.HttpContext,
                negado.Status == StatusCodes.Status401Unauthorized ? "nao-autorizado" : "proibido",
                negado.Detalhe);
            return Results.Problem(title: negado.Titulo, detail: negado.Detalhe, statusCode: negado.Status);
        }

        if (resultado is AcessoPermitido permitido)
        {
            context.HttpContext.Items[RecursoAuthorization.HttpItemUsuarioId] = permitido.UsuarioId;
            context.HttpContext.Items[RecursoAuthorization.HttpItemUsuarioNome] = permitido.Nome;
            context.HttpContext.Items[RecursoAuthorization.HttpItemUsuarioEmail] = permitido.Email;
            context.HttpContext.Items[RecursoAuthorization.HttpItemUsuarioJti] = permitido.TokenJti;
        }

        return await next(context);
    }
}

public sealed class UsuarioEscopoFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        if (context.HttpContext.Items[RecursoAuthorization.HttpItemUsuarioId] is not Guid chamador)
        {
            return Results.Problem(
                title: "Nao autorizado",
                detail: "Usuario do token nao identificado.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        var admins = context.HttpContext.RequestServices.GetRequiredService<IAdministradorSistemaRepository>();
        var ehAdmin = await admins.EhAdministradorAsync(chamador, context.HttpContext.RequestAborted);
        var alvo = LerIdDaRota(context.HttpContext);

        if (alvo is null)
        {
            if (ehAdmin)
            {
                return await next(context);
            }

            return Results.Problem(
                title: "Permissao insuficiente",
                detail: "Somente administradores em ADM_SYS podem listar ou criar outros usuarios.",
                statusCode: StatusCodes.Status403Forbidden);
        }

        if (alvo == chamador || ehAdmin)
        {
            return await next(context);
        }

        return Results.Problem(
            title: "Permissao insuficiente",
            detail: "Voce so pode operar o proprio cadastro. Para alterar outros usuarios, o ID precisa estar em ADM_SYS.",
            statusCode: StatusCodes.Status403Forbidden);
    }

    private static Guid? LerIdDaRota(HttpContext http)
    {
        var bruto = http.GetRouteValue("id")?.ToString();
        return Guid.TryParse(bruto, out var id) ? id : null;
    }
}

public sealed class AdministradorSistemaFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var verificador = context.HttpContext.RequestServices.GetRequiredService<IVerificadorAcesso>();
        var resultado = await verificador.VerificarAutenticadoAsync(
            context.HttpContext.Request.Headers.Authorization.ToString(),
            context.HttpContext.RequestAborted);

        if (resultado is AcessoNegado negado)
        {
            return Results.Problem(title: negado.Titulo, detail: negado.Detalhe, statusCode: negado.Status);
        }

        if (resultado is not AcessoPermitido permitido)
        {
            return Results.Problem(
                title: "Nao autorizado",
                detail: "Usuario do token nao identificado.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        context.HttpContext.Items[RecursoAuthorization.HttpItemUsuarioId] = permitido.UsuarioId;
        context.HttpContext.Items[RecursoAuthorization.HttpItemUsuarioNome] = permitido.Nome;
        context.HttpContext.Items[RecursoAuthorization.HttpItemUsuarioEmail] = permitido.Email;
        context.HttpContext.Items[RecursoAuthorization.HttpItemUsuarioJti] = permitido.TokenJti;

        var admins = context.HttpContext.RequestServices.GetRequiredService<IAdministradorSistemaRepository>();
        if (!await admins.EhAdministradorAsync(permitido.UsuarioId, context.HttpContext.RequestAborted))
        {
            return Results.Problem(
                title: "Permissao insuficiente",
                detail: "Somente usuarios em ADM_SYS podem administrar o cadastro de outros usuarios.",
                statusCode: StatusCodes.Status403Forbidden);
        }

        return await next(context);
    }
}
