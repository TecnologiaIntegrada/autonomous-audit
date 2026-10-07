using AutonomousAudit.Application.Data;
using AutonomousAudit.Domain;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using AutonomousAudit.Application;

namespace AutonomousAudit.Application.RequestHandlers;

public record ListarAdministradoresQuery : IQuery<ListarAdministradoresResult>;

public abstract record ListarAdministradoresResult
{
    public static ListarAdministradoresResult Ok(IReadOnlyList<AdministradorSistema> administradores) =>
        new ListarAdministradoresOk(administradores);

    public static ListarAdministradoresResult Fail(string message) => new ListarAdministradoresFail(message);
}

public record ListarAdministradoresOk(IReadOnlyList<AdministradorSistema> Administradores) : ListarAdministradoresResult;
public record ListarAdministradoresFail(string Message) : ListarAdministradoresResult;

public record IncluirAdministradorCommand(Guid UsuarioId) : ICommand<IncluirAdministradorResult>;

public abstract record IncluirAdministradorResult
{
    public static IncluirAdministradorResult Created(AdministradorSistema administrador) =>
        new IncluirAdministradorCreated(administrador);

    public static IncluirAdministradorResult NotFound(string message) => new IncluirAdministradorNotFound(message);
    public static IncluirAdministradorResult BadRequest(string message) => new IncluirAdministradorBadRequest(message);
    public static IncluirAdministradorResult Fail(string message) => new IncluirAdministradorFail(message);
}

public record IncluirAdministradorCreated(AdministradorSistema Administrador) : IncluirAdministradorResult;
public record IncluirAdministradorNotFound(string Message) : IncluirAdministradorResult;
public record IncluirAdministradorBadRequest(string Message) : IncluirAdministradorResult;
public record IncluirAdministradorFail(string Message) : IncluirAdministradorResult;

public record RemoverAdministradorCommand(Guid IdAdmin) : ICommand<RemoverAdministradorResult>;

public abstract record RemoverAdministradorResult
{
    public static RemoverAdministradorResult Ok() => new RemoverAdministradorOk();
    public static RemoverAdministradorResult NotFound(string message) => new RemoverAdministradorNotFound(message);
    public static RemoverAdministradorResult BadRequest(string message) => new RemoverAdministradorBadRequest(message);
    public static RemoverAdministradorResult Fail(string message) => new RemoverAdministradorFail(message);
}

public record RemoverAdministradorOk : RemoverAdministradorResult;
public record RemoverAdministradorNotFound(string Message) : RemoverAdministradorResult;
public record RemoverAdministradorBadRequest(string Message) : RemoverAdministradorResult;
public record RemoverAdministradorFail(string Message) : RemoverAdministradorResult;

public sealed class IncluirAdministradorCommandValidator : AbstractValidator<IncluirAdministradorCommand>
{
    public IncluirAdministradorCommandValidator() => RuleFor(x => x.UsuarioId).NotEmpty();
}

public sealed class RemoverAdministradorCommandValidator : AbstractValidator<RemoverAdministradorCommand>
{
    public RemoverAdministradorCommandValidator() => RuleFor(x => x.IdAdmin).NotEmpty();
}

public sealed class ListarAdministradoresHandler : IRequestHandler<ListarAdministradoresQuery, ListarAdministradoresResult>
{
    private readonly IAdministradorSistemaRepository _admins;
    private readonly ILogger<ListarAdministradoresHandler> _logger;

    public ListarAdministradoresHandler(
        IAdministradorSistemaRepository admins,
        ILogger<ListarAdministradoresHandler> logger)
    {
        _admins = admins;
        _logger = logger;
    }

    public async Task<ListarAdministradoresResult> Handle(
        ListarAdministradoresQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            var lista = await _admins.ListarAsync(cancellationToken);
            return ListarAdministradoresResult.Ok(lista);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao listar ADM_SYS");
            return ListarAdministradoresResult.Fail(ex.Message);
        }
    }
}

public sealed class IncluirAdministradorHandler : IRequestHandler<IncluirAdministradorCommand, IncluirAdministradorResult>
{
    private readonly IAdministradorSistemaRepository _admins;
    private readonly IUsuarioRepository _usuarios;
    private readonly ILogger<IncluirAdministradorHandler> _logger;

    public IncluirAdministradorHandler(
        IAdministradorSistemaRepository admins,
        IUsuarioRepository usuarios,
        ILogger<IncluirAdministradorHandler> logger)
    {
        _admins = admins;
        _usuarios = usuarios;
        _logger = logger;
    }

    public async Task<IncluirAdministradorResult> Handle(
        IncluirAdministradorCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            var usuario = await _usuarios.GetByIdAsync(request.UsuarioId, cancellationToken);
            if (usuario is null)
            {
                return IncluirAdministradorResult.NotFound("Usuario nao encontrado.");
            }

            if (await _admins.GetByUsuarioIdAsync(request.UsuarioId, cancellationToken) is not null)
            {
                return IncluirAdministradorResult.BadRequest("Este usuario ja esta em ADM_SYS.");
            }

            var administrador = new AdministradorSistema(request.UsuarioId);
            await _admins.AddAsync(administrador, cancellationToken);
            await _admins.SaveChangesAsync(cancellationToken);
            administrador = await _admins.GetByIdAsync(administrador.Id, cancellationToken) ?? administrador;
            _logger.LogInformation(
                "Usuario {UsuarioId} incluido em ADM_SYS como {IdAdmin}",
                request.UsuarioId,
                administrador.Id);
            return IncluirAdministradorResult.Created(administrador);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao incluir usuario {UsuarioId} em ADM_SYS", request.UsuarioId);
            return IncluirAdministradorResult.Fail(ex.Message);
        }
    }
}

public sealed class RemoverAdministradorHandler : IRequestHandler<RemoverAdministradorCommand, RemoverAdministradorResult>
{
    private readonly IAdministradorSistemaRepository _admins;
    private readonly ILogger<RemoverAdministradorHandler> _logger;

    public RemoverAdministradorHandler(
        IAdministradorSistemaRepository admins,
        ILogger<RemoverAdministradorHandler> logger)
    {
        _admins = admins;
        _logger = logger;
    }

    public async Task<RemoverAdministradorResult> Handle(
        RemoverAdministradorCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            var administrador = await _admins.GetByIdAsync(request.IdAdmin, cancellationToken);
            if (administrador is null)
            {
                return RemoverAdministradorResult.NotFound("Administrador nao encontrado em ADM_SYS.");
            }

            if (await _admins.ContarAsync(cancellationToken) <= 1)
            {
                return RemoverAdministradorResult.BadRequest(
                    "Nao e possivel remover o ultimo administrador do sistema (ADM_SYS).");
            }

            _admins.Remove(administrador);
            await _admins.SaveChangesAsync(cancellationToken);
            _logger.LogInformation(
                "Usuario {UsuarioId} removido de ADM_SYS ({IdAdmin})",
                administrador.UsuarioId,
                administrador.Id);
            return RemoverAdministradorResult.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao remover ADM_SYS {IdAdmin}", request.IdAdmin);
            return RemoverAdministradorResult.Fail(ex.Message);
        }
    }
}
