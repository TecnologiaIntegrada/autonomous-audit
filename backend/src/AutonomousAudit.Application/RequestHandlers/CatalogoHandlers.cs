using AutonomousAudit.Application.Data;
using AutonomousAudit.Domain;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AutonomousAudit.Application.RequestHandlers;

public record ObterModuloQuery(Guid Id) : IQuery<ObterModuloResult>;

public abstract record ObterModuloResult
{
    public static ObterModuloResult Ok(Modulo modulo) => new ObterModuloOk(modulo);
    public static ObterModuloResult NotFound(string message) => new ObterModuloNotFound(message);
    public static ObterModuloResult Fail(string message) => new ObterModuloFail(message);
}

public record ObterModuloOk(Modulo Modulo) : ObterModuloResult;
public record ObterModuloNotFound(string Message) : ObterModuloResult;
public record ObterModuloFail(string Message) : ObterModuloResult;

public record CriarModuloCommand(string Codigo, string Nome, string Descricao) : ICommand<AlterarModuloResult>;
public record AtualizarModuloCommand(Guid Id, string Codigo, string Nome, string Descricao) : ICommand<AlterarModuloResult>;
public record ExcluirModuloCommand(Guid Id) : ICommand<ExcluirCatalogoResult>;

public abstract record AlterarModuloResult
{
    public static AlterarModuloResult Ok(Modulo modulo) => new AlterarModuloOk(modulo);
    public static AlterarModuloResult Created(Modulo modulo) => new AlterarModuloCreated(modulo);
    public static AlterarModuloResult NotFound(string message) => new AlterarModuloNotFound(message);
    public static AlterarModuloResult BadRequest(string message) => new AlterarModuloBadRequest(message);
    public static AlterarModuloResult Fail(string message) => new AlterarModuloFail(message);
}

public record AlterarModuloOk(Modulo Modulo) : AlterarModuloResult;
public record AlterarModuloCreated(Modulo Modulo) : AlterarModuloResult;
public record AlterarModuloNotFound(string Message) : AlterarModuloResult;
public record AlterarModuloBadRequest(string Message) : AlterarModuloResult;
public record AlterarModuloFail(string Message) : AlterarModuloResult;

public abstract record ExcluirCatalogoResult
{
    public static ExcluirCatalogoResult Ok() => new ExcluirCatalogoOk();
    public static ExcluirCatalogoResult NotFound(string message) => new ExcluirCatalogoNotFound(message);
    public static ExcluirCatalogoResult Fail(string message) => new ExcluirCatalogoFail(message);
}

public record ExcluirCatalogoOk : ExcluirCatalogoResult;
public record ExcluirCatalogoNotFound(string Message) : ExcluirCatalogoResult;
public record ExcluirCatalogoFail(string Message) : ExcluirCatalogoResult;

public record ListarRecursosQuery(Guid? ModuloId) : IQuery<ListarRecursosResult>;

public abstract record ListarRecursosResult
{
    public static ListarRecursosResult Ok(IReadOnlyList<Recurso> recursos) => new ListarRecursosOk(recursos);
    public static ListarRecursosResult Fail(string message) => new ListarRecursosFail(message);
}

public record ListarRecursosOk(IReadOnlyList<Recurso> Recursos) : ListarRecursosResult;
public record ListarRecursosFail(string Message) : ListarRecursosResult;

public record ObterRecursoQuery(Guid Id) : IQuery<ObterRecursoResult>;

public abstract record ObterRecursoResult
{
    public static ObterRecursoResult Ok(Recurso recurso) => new ObterRecursoOk(recurso);
    public static ObterRecursoResult NotFound(string message) => new ObterRecursoNotFound(message);
    public static ObterRecursoResult Fail(string message) => new ObterRecursoFail(message);
}

public record ObterRecursoOk(Recurso Recurso) : ObterRecursoResult;
public record ObterRecursoNotFound(string Message) : ObterRecursoResult;
public record ObterRecursoFail(string Message) : ObterRecursoResult;

public record CriarRecursoCommand(
    Guid ModuloId,
    string Codigo,
    string Nome,
    string? MetodoHttp,
    string? Rota,
    string? Chave) : ICommand<AlterarRecursoResult>;

public record AtualizarRecursoCommand(
    Guid Id,
    Guid ModuloId,
    string Codigo,
    string Nome,
    string? MetodoHttp,
    string? Rota,
    string? Chave) : ICommand<AlterarRecursoResult>;

public record ExcluirRecursoCommand(Guid Id) : ICommand<ExcluirCatalogoResult>;

public abstract record AlterarRecursoResult
{
    public static AlterarRecursoResult Ok(Recurso recurso) => new AlterarRecursoOk(recurso);
    public static AlterarRecursoResult Created(Recurso recurso) => new AlterarRecursoCreated(recurso);
    public static AlterarRecursoResult NotFound(string message) => new AlterarRecursoNotFound(message);
    public static AlterarRecursoResult BadRequest(string message) => new AlterarRecursoBadRequest(message);
    public static AlterarRecursoResult Fail(string message) => new AlterarRecursoFail(message);
}

public record AlterarRecursoOk(Recurso Recurso) : AlterarRecursoResult;
public record AlterarRecursoCreated(Recurso Recurso) : AlterarRecursoResult;
public record AlterarRecursoNotFound(string Message) : AlterarRecursoResult;
public record AlterarRecursoBadRequest(string Message) : AlterarRecursoResult;
public record AlterarRecursoFail(string Message) : AlterarRecursoResult;

public sealed class ObterModuloQueryValidator : AbstractValidator<ObterModuloQuery>
{
    public ObterModuloQueryValidator() => RuleFor(x => x.Id).NotEmpty();
}

public sealed class CriarModuloCommandValidator : AbstractValidator<CriarModuloCommand>
{
    public CriarModuloCommandValidator()
    {
        RuleFor(x => x.Codigo).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Descricao).MaximumLength(300);
    }
}

public sealed class AtualizarModuloCommandValidator : AbstractValidator<AtualizarModuloCommand>
{
    public AtualizarModuloCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Codigo).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Descricao).MaximumLength(300);
    }
}

public sealed class ExcluirModuloCommandValidator : AbstractValidator<ExcluirModuloCommand>
{
    public ExcluirModuloCommandValidator() => RuleFor(x => x.Id).NotEmpty();
}

public sealed class ObterRecursoQueryValidator : AbstractValidator<ObterRecursoQuery>
{
    public ObterRecursoQueryValidator() => RuleFor(x => x.Id).NotEmpty();
}

public sealed class CriarRecursoCommandValidator : AbstractValidator<CriarRecursoCommand>
{
    public CriarRecursoCommandValidator()
    {
        RuleFor(x => x.ModuloId).NotEmpty();
        RuleFor(x => x.Codigo).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(200);
        RuleFor(x => x.MetodoHttp).MaximumLength(10);
        RuleFor(x => x.Rota).MaximumLength(200);
        RuleFor(x => x.Chave).MaximumLength(80);
    }
}

public sealed class AtualizarRecursoCommandValidator : AbstractValidator<AtualizarRecursoCommand>
{
    public AtualizarRecursoCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.ModuloId).NotEmpty();
        RuleFor(x => x.Codigo).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(200);
        RuleFor(x => x.MetodoHttp).MaximumLength(10);
        RuleFor(x => x.Rota).MaximumLength(200);
        RuleFor(x => x.Chave).MaximumLength(80);
    }
}

public sealed class ExcluirRecursoCommandValidator : AbstractValidator<ExcluirRecursoCommand>
{
    public ExcluirRecursoCommandValidator() => RuleFor(x => x.Id).NotEmpty();
}

public sealed class ObterModuloHandler : IRequestHandler<ObterModuloQuery, ObterModuloResult>
{
    private readonly ICatalogoConsulta _catalogo;
    private readonly ILogger<ObterModuloHandler> _logger;

    public ObterModuloHandler(ICatalogoConsulta catalogo, ILogger<ObterModuloHandler> logger)
    {
        _catalogo = catalogo;
        _logger = logger;
    }

    public async Task<ObterModuloResult> Handle(ObterModuloQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var modulo = await _catalogo.ObterModuloPorIdAsync(request.Id, cancellationToken);
            return modulo is null
                ? ObterModuloResult.NotFound("Modulo nao encontrado.")
                : ObterModuloResult.Ok(modulo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao obter modulo {Id}", request.Id);
            return ObterModuloResult.Fail(ex.Message);
        }
    }
}

public sealed class CriarModuloHandler : IRequestHandler<CriarModuloCommand, AlterarModuloResult>
{
    private readonly ICatalogoPermissaoRepository _catalogo;
    private readonly ILogger<CriarModuloHandler> _logger;

    public CriarModuloHandler(ICatalogoPermissaoRepository catalogo, ILogger<CriarModuloHandler> logger)
    {
        _catalogo = catalogo;
        _logger = logger;
    }

    public async Task<AlterarModuloResult> Handle(CriarModuloCommand request, CancellationToken cancellationToken)
    {
        try
        {
            if (await _catalogo.CodigoModuloEmUsoAsync(request.Codigo, null, cancellationToken))
            {
                return AlterarModuloResult.BadRequest("Ja existe um modulo com este codigo.");
            }

            var modulo = new Modulo(request.Codigo, request.Nome, request.Descricao ?? string.Empty);
            await _catalogo.AddModuloAsync(modulo, cancellationToken);
            await _catalogo.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Modulo criado {Codigo} {Id}", modulo.Codigo, modulo.Id);
            return AlterarModuloResult.Created(modulo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao criar modulo {Codigo}", request.Codigo);
            return AlterarModuloResult.Fail(ex.Message);
        }
    }
}

public sealed class AtualizarModuloHandler : IRequestHandler<AtualizarModuloCommand, AlterarModuloResult>
{
    private readonly ICatalogoPermissaoRepository _catalogo;
    private readonly ILogger<AtualizarModuloHandler> _logger;

    public AtualizarModuloHandler(ICatalogoPermissaoRepository catalogo, ILogger<AtualizarModuloHandler> logger)
    {
        _catalogo = catalogo;
        _logger = logger;
    }

    public async Task<AlterarModuloResult> Handle(AtualizarModuloCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var modulo = await _catalogo.ObterModuloPorIdAsync(request.Id, cancellationToken);
            if (modulo is null)
            {
                return AlterarModuloResult.NotFound("Modulo nao encontrado.");
            }

            if (await _catalogo.CodigoModuloEmUsoAsync(request.Codigo, modulo.Id, cancellationToken))
            {
                return AlterarModuloResult.BadRequest("Ja existe um modulo com este codigo.");
            }

            modulo.Atualizar(request.Nome, request.Descricao ?? string.Empty);
            modulo.DefinirCodigo(request.Codigo);
            await _catalogo.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Modulo atualizado {Id}", modulo.Id);
            return AlterarModuloResult.Ok(modulo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao atualizar modulo {Id}", request.Id);
            return AlterarModuloResult.Fail(ex.Message);
        }
    }
}

public sealed class ExcluirModuloHandler : IRequestHandler<ExcluirModuloCommand, ExcluirCatalogoResult>
{
    private readonly ICatalogoPermissaoRepository _catalogo;
    private readonly ILogger<ExcluirModuloHandler> _logger;

    public ExcluirModuloHandler(ICatalogoPermissaoRepository catalogo, ILogger<ExcluirModuloHandler> logger)
    {
        _catalogo = catalogo;
        _logger = logger;
    }

    public async Task<ExcluirCatalogoResult> Handle(ExcluirModuloCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var modulo = await _catalogo.ObterModuloPorIdAsync(request.Id, cancellationToken);
            if (modulo is null)
            {
                return ExcluirCatalogoResult.NotFound("Modulo nao encontrado.");
            }

            await _catalogo.RemoverPermissoesDoModuloAsync(modulo.Id, cancellationToken);
            _catalogo.RemoveModulo(modulo);
            await _catalogo.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Modulo excluido {Id}", request.Id);
            return ExcluirCatalogoResult.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao excluir modulo {Id}", request.Id);
            return ExcluirCatalogoResult.Fail(ex.Message);
        }
    }
}

public sealed class ListarRecursosHandler : IRequestHandler<ListarRecursosQuery, ListarRecursosResult>
{
    private readonly ICatalogoConsulta _catalogo;
    private readonly ILogger<ListarRecursosHandler> _logger;

    public ListarRecursosHandler(ICatalogoConsulta catalogo, ILogger<ListarRecursosHandler> logger)
    {
        _catalogo = catalogo;
        _logger = logger;
    }

    public async Task<ListarRecursosResult> Handle(ListarRecursosQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var recursos = await _catalogo.ListarRecursosAsync(request.ModuloId, cancellationToken);
            return ListarRecursosResult.Ok(recursos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao listar recursos");
            return ListarRecursosResult.Fail(ex.Message);
        }
    }
}

public sealed class ObterRecursoHandler : IRequestHandler<ObterRecursoQuery, ObterRecursoResult>
{
    private readonly ICatalogoConsulta _catalogo;
    private readonly ILogger<ObterRecursoHandler> _logger;

    public ObterRecursoHandler(ICatalogoConsulta catalogo, ILogger<ObterRecursoHandler> logger)
    {
        _catalogo = catalogo;
        _logger = logger;
    }

    public async Task<ObterRecursoResult> Handle(ObterRecursoQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var recurso = await _catalogo.ObterRecursoPorIdAsync(request.Id, cancellationToken);
            return recurso is null
                ? ObterRecursoResult.NotFound("Recurso nao encontrado.")
                : ObterRecursoResult.Ok(recurso);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao obter recurso {Id}", request.Id);
            return ObterRecursoResult.Fail(ex.Message);
        }
    }
}

public sealed class CriarRecursoHandler : IRequestHandler<CriarRecursoCommand, AlterarRecursoResult>
{
    private readonly ICatalogoPermissaoRepository _catalogo;
    private readonly ILogger<CriarRecursoHandler> _logger;

    public CriarRecursoHandler(ICatalogoPermissaoRepository catalogo, ILogger<CriarRecursoHandler> logger)
    {
        _catalogo = catalogo;
        _logger = logger;
    }

    public async Task<AlterarRecursoResult> Handle(CriarRecursoCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var modulo = await _catalogo.ObterModuloPorIdAsync(request.ModuloId, cancellationToken);
            if (modulo is null)
            {
                return AlterarRecursoResult.BadRequest("Modulo nao encontrado.");
            }

            var chave = string.IsNullOrWhiteSpace(request.Chave)
                ? $"{modulo.Codigo}.{request.Codigo.Trim().ToLowerInvariant()}"
                : request.Chave.Trim().ToLowerInvariant();
            if (await _catalogo.ChaveRecursoEmUsoAsync(chave, null, cancellationToken))
            {
                return AlterarRecursoResult.BadRequest("Ja existe um recurso com esta chave.");
            }

            var recurso = new Recurso(
                modulo.Id,
                request.Codigo,
                chave,
                request.Nome,
                request.MetodoHttp ?? string.Empty,
                request.Rota ?? string.Empty);
            await _catalogo.AddRecursoAsync(recurso, cancellationToken);
            await _catalogo.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Recurso criado {Chave} {Id}", recurso.Chave, recurso.Id);
            return AlterarRecursoResult.Created(recurso);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao criar recurso");
            return AlterarRecursoResult.Fail(ex.Message);
        }
    }
}

public sealed class AtualizarRecursoHandler : IRequestHandler<AtualizarRecursoCommand, AlterarRecursoResult>
{
    private readonly ICatalogoPermissaoRepository _catalogo;
    private readonly ILogger<AtualizarRecursoHandler> _logger;

    public AtualizarRecursoHandler(ICatalogoPermissaoRepository catalogo, ILogger<AtualizarRecursoHandler> logger)
    {
        _catalogo = catalogo;
        _logger = logger;
    }

    public async Task<AlterarRecursoResult> Handle(AtualizarRecursoCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var recurso = await _catalogo.ObterPorIdAsync(request.Id, cancellationToken);
            if (recurso is null)
            {
                return AlterarRecursoResult.NotFound("Recurso nao encontrado.");
            }

            var modulo = await _catalogo.ObterModuloPorIdAsync(request.ModuloId, cancellationToken);
            if (modulo is null)
            {
                return AlterarRecursoResult.BadRequest("Modulo nao encontrado.");
            }

            var chave = string.IsNullOrWhiteSpace(request.Chave)
                ? $"{modulo.Codigo}.{request.Codigo.Trim().ToLowerInvariant()}"
                : request.Chave.Trim().ToLowerInvariant();
            if (await _catalogo.ChaveRecursoEmUsoAsync(chave, recurso.Id, cancellationToken))
            {
                return AlterarRecursoResult.BadRequest("Ja existe um recurso com esta chave.");
            }

            recurso.Atualizar(request.Nome, request.MetodoHttp ?? string.Empty, request.Rota ?? string.Empty);
            recurso.DefinirModulo(modulo.Id);
            recurso.DefinirCodigo(request.Codigo);
            recurso.DefinirChave(chave);
            await _catalogo.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Recurso atualizado {Id}", recurso.Id);
            return AlterarRecursoResult.Ok(recurso);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao atualizar recurso {Id}", request.Id);
            return AlterarRecursoResult.Fail(ex.Message);
        }
    }
}

public sealed class ExcluirRecursoHandler : IRequestHandler<ExcluirRecursoCommand, ExcluirCatalogoResult>
{
    private readonly ICatalogoPermissaoRepository _catalogo;
    private readonly ILogger<ExcluirRecursoHandler> _logger;

    public ExcluirRecursoHandler(ICatalogoPermissaoRepository catalogo, ILogger<ExcluirRecursoHandler> logger)
    {
        _catalogo = catalogo;
        _logger = logger;
    }

    public async Task<ExcluirCatalogoResult> Handle(ExcluirRecursoCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var recurso = await _catalogo.ObterPorIdAsync(request.Id, cancellationToken);
            if (recurso is null)
            {
                return ExcluirCatalogoResult.NotFound("Recurso nao encontrado.");
            }

            await _catalogo.RemoverPermissoesDoRecursoAsync(recurso.Id, cancellationToken);
            _catalogo.RemoveRecurso(recurso);
            await _catalogo.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Recurso excluido {Id}", request.Id);
            return ExcluirCatalogoResult.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao excluir recurso {Id}", request.Id);
            return ExcluirCatalogoResult.Fail(ex.Message);
        }
    }
}
