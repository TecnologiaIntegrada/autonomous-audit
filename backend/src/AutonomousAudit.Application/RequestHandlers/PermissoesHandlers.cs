using AutonomousAudit.Application.Data;
using AutonomousAudit.Domain;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AutonomousAudit.Application.RequestHandlers;

public record ListarModulosQuery : IQuery<ListarModulosResult>;

public abstract record ListarModulosResult
{
    public static ListarModulosResult Ok(IReadOnlyList<Modulo> modulos) => new ListarModulosOk(modulos);
    public static ListarModulosResult Fail(string message) => new ListarModulosFail(message);
}

public record ListarModulosOk(IReadOnlyList<Modulo> Modulos) : ListarModulosResult;
public record ListarModulosFail(string Message) : ListarModulosResult;

public record ListarPermissoesUsuarioQuery(Guid UsuarioId) : IQuery<ListarPermissoesUsuarioResult>;

public abstract record ListarPermissoesUsuarioResult
{
    public static ListarPermissoesUsuarioResult Ok(Usuario usuario) => new ListarPermissoesUsuarioOk(usuario);
    public static ListarPermissoesUsuarioResult NotFound(string message) => new ListarPermissoesUsuarioNotFound(message);
    public static ListarPermissoesUsuarioResult Fail(string message) => new ListarPermissoesUsuarioFail(message);
}

public record ListarPermissoesUsuarioOk(Usuario Usuario) : ListarPermissoesUsuarioResult;
public record ListarPermissoesUsuarioNotFound(string Message) : ListarPermissoesUsuarioResult;
public record ListarPermissoesUsuarioFail(string Message) : ListarPermissoesUsuarioResult;

public record SubstituirPermissoesCommand(Guid UsuarioId, IReadOnlyList<string> Recursos)
    : ICommand<AlterarPermissoesResult>;

public record AdicionarPermissaoCommand(Guid UsuarioId, string Recurso) : ICommand<AlterarPermissoesResult>;

public record RemoverPermissaoCommand(Guid UsuarioId, Guid RecursoId) : ICommand<AlterarPermissoesResult>;

public abstract record AlterarPermissoesResult
{
    public static AlterarPermissoesResult Ok(Usuario usuario) => new AlterarPermissoesOk(usuario);
    public static AlterarPermissoesResult NotFound(string message) => new AlterarPermissoesNotFound(message);
    public static AlterarPermissoesResult BadRequest(string message) => new AlterarPermissoesBadRequest(message);
    public static AlterarPermissoesResult Fail(string message) => new AlterarPermissoesFail(message);
}

public record AlterarPermissoesOk(Usuario Usuario) : AlterarPermissoesResult;
public record AlterarPermissoesNotFound(string Message) : AlterarPermissoesResult;
public record AlterarPermissoesBadRequest(string Message) : AlterarPermissoesResult;
public record AlterarPermissoesFail(string Message) : AlterarPermissoesResult;

public sealed class ListarPermissoesUsuarioQueryValidator : AbstractValidator<ListarPermissoesUsuarioQuery>
{
    public ListarPermissoesUsuarioQueryValidator() => RuleFor(x => x.UsuarioId).NotEmpty();
}

public sealed class SubstituirPermissoesCommandValidator : AbstractValidator<SubstituirPermissoesCommand>
{
    public SubstituirPermissoesCommandValidator()
    {
        RuleFor(x => x.UsuarioId).NotEmpty();
        RuleFor(x => x.Recursos).NotNull();
    }
}

public sealed class AdicionarPermissaoCommandValidator : AbstractValidator<AdicionarPermissaoCommand>
{
    public AdicionarPermissaoCommandValidator()
    {
        RuleFor(x => x.UsuarioId).NotEmpty();
        RuleFor(x => x.Recurso).NotEmpty();
    }
}

public sealed class RemoverPermissaoCommandValidator : AbstractValidator<RemoverPermissaoCommand>
{
    public RemoverPermissaoCommandValidator()
    {
        RuleFor(x => x.UsuarioId).NotEmpty();
        RuleFor(x => x.RecursoId).NotEmpty();
    }
}

public sealed class ListarModulosHandler : IRequestHandler<ListarModulosQuery, ListarModulosResult>
{
    private readonly ICatalogoConsulta _catalogo;
    private readonly ILogger<ListarModulosHandler> _logger;

    public ListarModulosHandler(ICatalogoConsulta catalogo, ILogger<ListarModulosHandler> logger)
    {
        _catalogo = catalogo;
        _logger = logger;
    }

    public async Task<ListarModulosResult> Handle(ListarModulosQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var modulos = await _catalogo.ListarModulosComRecursosAsync(cancellationToken);
            return ListarModulosResult.Ok(modulos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao listar modulos");
            return ListarModulosResult.Fail(ex.Message);
        }
    }
}

public sealed class ListarPermissoesUsuarioHandler : IRequestHandler<ListarPermissoesUsuarioQuery, ListarPermissoesUsuarioResult>
{
    private readonly IUsuarioConsulta _usuarios;
    private readonly ILogger<ListarPermissoesUsuarioHandler> _logger;

    public ListarPermissoesUsuarioHandler(IUsuarioConsulta usuarios, ILogger<ListarPermissoesUsuarioHandler> logger)
    {
        _usuarios = usuarios;
        _logger = logger;
    }

    public async Task<ListarPermissoesUsuarioResult> Handle(
        ListarPermissoesUsuarioQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            var usuario = await _usuarios.ObterPorIdAsync(request.UsuarioId, incluirPermissoes: true, cancellationToken);
            return usuario is null
                ? ListarPermissoesUsuarioResult.NotFound("Usuario nao encontrado.")
                : ListarPermissoesUsuarioResult.Ok(usuario);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao listar permissoes do usuario {UsuarioId}", request.UsuarioId);
            return ListarPermissoesUsuarioResult.Fail(ex.Message);
        }
    }
}

public sealed class SubstituirPermissoesHandler : IRequestHandler<SubstituirPermissoesCommand, AlterarPermissoesResult>
{
    private readonly IUsuarioRepository _usuarios;
    private readonly ICatalogoPermissaoRepository _catalogo;
    private readonly ILogger<SubstituirPermissoesHandler> _logger;

    public SubstituirPermissoesHandler(
        IUsuarioRepository usuarios,
        ICatalogoPermissaoRepository catalogo,
        ILogger<SubstituirPermissoesHandler> logger)
    {
        _usuarios = usuarios;
        _catalogo = catalogo;
        _logger = logger;
    }

    public async Task<AlterarPermissoesResult> Handle(SubstituirPermissoesCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var usuario = await _usuarios.GetByIdComPermissoesAsync(request.UsuarioId, cancellationToken);
            if (usuario is null)
            {
                return AlterarPermissoesResult.NotFound("Usuario nao encontrado.");
            }

            var chaves = request.Recursos.Select(c => c.Trim().ToLowerInvariant()).Where(c => c.Length > 0).Distinct().ToList();
            var recursos = await _catalogo.ObterPorChavesAsync(chaves, cancellationToken);
            var faltando = chaves.Except(recursos.Select(r => r.Chave), StringComparer.OrdinalIgnoreCase).ToList();
            if (faltando.Count > 0)
            {
                return AlterarPermissoesResult.BadRequest("Recursos desconhecidos: " + string.Join(", ", faltando));
            }

            usuario.SubstituirPermissoes(recursos.Select(r => (r.ModuloId, r.Id)));
            await _usuarios.SaveChangesAsync(cancellationToken);
            usuario = await _usuarios.GetByIdComPermissoesAsync(usuario.Id, cancellationToken) ?? usuario;
            _logger.LogInformation("Permissoes substituidas usuario={UsuarioId} quantidade={Qtd}", usuario.Id, recursos.Count);
            return AlterarPermissoesResult.Ok(usuario);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao substituir permissoes usuario={UsuarioId}", request.UsuarioId);
            return AlterarPermissoesResult.Fail(ex.Message);
        }
    }
}

public sealed class AdicionarPermissaoHandler : IRequestHandler<AdicionarPermissaoCommand, AlterarPermissoesResult>
{
    private readonly IUsuarioRepository _usuarios;
    private readonly ICatalogoPermissaoRepository _catalogo;
    private readonly ILogger<AdicionarPermissaoHandler> _logger;

    public AdicionarPermissaoHandler(
        IUsuarioRepository usuarios,
        ICatalogoPermissaoRepository catalogo,
        ILogger<AdicionarPermissaoHandler> logger)
    {
        _usuarios = usuarios;
        _catalogo = catalogo;
        _logger = logger;
    }

    public async Task<AlterarPermissoesResult> Handle(AdicionarPermissaoCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var usuario = await _usuarios.GetByIdComPermissoesAsync(request.UsuarioId, cancellationToken);
            if (usuario is null)
            {
                return AlterarPermissoesResult.NotFound("Usuario nao encontrado.");
            }

            var recurso = await _catalogo.ObterPorChaveAsync(request.Recurso, cancellationToken);
            if (recurso is null)
            {
                return AlterarPermissoesResult.BadRequest("Recurso desconhecido: " + request.Recurso);
            }

            usuario.AdicionarPermissao(recurso.ModuloId, recurso.Id);
            await _usuarios.SaveChangesAsync(cancellationToken);
            usuario = await _usuarios.GetByIdComPermissoesAsync(usuario.Id, cancellationToken) ?? usuario;
            _logger.LogInformation("Permissao {Chave} atribuida a {UsuarioId}", recurso.Chave, usuario.Id);
            return AlterarPermissoesResult.Ok(usuario);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao atribuir permissao usuario={UsuarioId}", request.UsuarioId);
            return AlterarPermissoesResult.Fail(ex.Message);
        }
    }
}

public sealed class RemoverPermissaoHandler : IRequestHandler<RemoverPermissaoCommand, AlterarPermissoesResult>
{
    private readonly IUsuarioRepository _usuarios;
    private readonly ILogger<RemoverPermissaoHandler> _logger;

    public RemoverPermissaoHandler(IUsuarioRepository usuarios, ILogger<RemoverPermissaoHandler> logger)
    {
        _usuarios = usuarios;
        _logger = logger;
    }

    public async Task<AlterarPermissoesResult> Handle(RemoverPermissaoCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var usuario = await _usuarios.GetByIdComPermissoesAsync(request.UsuarioId, cancellationToken);
            if (usuario is null)
            {
                return AlterarPermissoesResult.NotFound("Usuario nao encontrado.");
            }

            usuario.RemoverPermissao(request.RecursoId);
            await _usuarios.SaveChangesAsync(cancellationToken);
            usuario = await _usuarios.GetByIdComPermissoesAsync(usuario.Id, cancellationToken) ?? usuario;
            _logger.LogInformation("Permissao {RecursoId} revogada de {UsuarioId}", request.RecursoId, usuario.Id);
            return AlterarPermissoesResult.Ok(usuario);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao revogar permissao usuario={UsuarioId}", request.UsuarioId);
            return AlterarPermissoesResult.Fail(ex.Message);
        }
    }
}
