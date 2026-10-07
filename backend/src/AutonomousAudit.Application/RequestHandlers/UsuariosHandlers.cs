using AutonomousAudit.Application.Data;
using AutonomousAudit.Domain;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using AutonomousAudit.Application;

namespace AutonomousAudit.Application.RequestHandlers;

public record DadosUsuario(
    string Nome,
    string EmailPrincipal,
    string? EmailSecundario,
    string? TelefonePrincipal,
    string? TelefoneSecundario,
    string? TipoDocto,
    string? NDocto,
    DateOnly? DataEmissao,
    string? Orgao,
    string? Cidade,
    string? Endereco,
    string? Cep,
    string? Uf,
    string? Pais,
    bool SmsAuth,
    bool EmailAuth);

public record CriarUsuarioCommand(
    DadosUsuario Dados,
    string Senha,
    IReadOnlyList<string>? Recursos) : ICommand<CriarUsuarioResult>;

public abstract record CriarUsuarioResult
{
    public static CriarUsuarioResult Created(Usuario usuario) => new CriarUsuarioCreated(usuario);
    public static CriarUsuarioResult BadRequest(string message) => new CriarUsuarioBadRequest(message);
    public static CriarUsuarioResult Fail(string message) => new CriarUsuarioFail(message);
}

public record CriarUsuarioCreated(Usuario Usuario) : CriarUsuarioResult;
public record CriarUsuarioBadRequest(string Message) : CriarUsuarioResult;
public record CriarUsuarioFail(string Message) : CriarUsuarioResult;

public record AtualizarUsuarioCommand(Guid Id, DadosUsuario Dados, string? Senha) : ICommand<AtualizarUsuarioResult>;

public abstract record AtualizarUsuarioResult
{
    public static AtualizarUsuarioResult Ok(Usuario usuario) => new AtualizarUsuarioOk(usuario);
    public static AtualizarUsuarioResult NotFound(string message) => new AtualizarUsuarioNotFound(message);
    public static AtualizarUsuarioResult BadRequest(string message) => new AtualizarUsuarioBadRequest(message);
    public static AtualizarUsuarioResult Fail(string message) => new AtualizarUsuarioFail(message);
}

public record AtualizarUsuarioOk(Usuario Usuario) : AtualizarUsuarioResult;
public record AtualizarUsuarioNotFound(string Message) : AtualizarUsuarioResult;
public record AtualizarUsuarioBadRequest(string Message) : AtualizarUsuarioResult;
public record AtualizarUsuarioFail(string Message) : AtualizarUsuarioResult;

public record ExcluirUsuarioCommand(Guid Id) : ICommand<ExcluirUsuarioResult>;

public abstract record ExcluirUsuarioResult
{
    public static ExcluirUsuarioResult Ok() => new ExcluirUsuarioOk();
    public static ExcluirUsuarioResult NotFound(string message) => new ExcluirUsuarioNotFound(message);
    public static ExcluirUsuarioResult BadRequest(string message) => new ExcluirUsuarioBadRequest(message);
    public static ExcluirUsuarioResult Fail(string message) => new ExcluirUsuarioFail(message);
}

public record ExcluirUsuarioOk : ExcluirUsuarioResult;
public record ExcluirUsuarioNotFound(string Message) : ExcluirUsuarioResult;
public record ExcluirUsuarioBadRequest(string Message) : ExcluirUsuarioResult;
public record ExcluirUsuarioFail(string Message) : ExcluirUsuarioResult;

public record ObterUsuarioQuery(Guid Id) : IQuery<ObterUsuarioResult>;

public abstract record ObterUsuarioResult
{
    public static ObterUsuarioResult Ok(Usuario usuario) => new ObterUsuarioOk(usuario);
    public static ObterUsuarioResult NotFound(string message) => new ObterUsuarioNotFound(message);
    public static ObterUsuarioResult Fail(string message) => new ObterUsuarioFail(message);
}

public record ObterUsuarioOk(Usuario Usuario) : ObterUsuarioResult;
public record ObterUsuarioNotFound(string Message) : ObterUsuarioResult;
public record ObterUsuarioFail(string Message) : ObterUsuarioResult;

public record ListarUsuariosQuery : IQuery<ListarUsuariosResult>;

public abstract record ListarUsuariosResult
{
    public static ListarUsuariosResult Ok(IReadOnlyList<Usuario> usuarios) => new ListarUsuariosOk(usuarios);
    public static ListarUsuariosResult Fail(string message) => new ListarUsuariosFail(message);
}

public record ListarUsuariosOk(IReadOnlyList<Usuario> Usuarios) : ListarUsuariosResult;
public record ListarUsuariosFail(string Message) : ListarUsuariosResult;

public sealed class DadosUsuarioValidator : AbstractValidator<DadosUsuario>
{
    public DadosUsuarioValidator()
    {
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(200);
        RuleFor(x => x.EmailPrincipal).NotEmpty().EmailAddress().MaximumLength(320);
        RuleFor(x => x.EmailSecundario).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.EmailSecundario));
        RuleFor(x => x.TelefonePrincipal).MaximumLength(30);
        RuleFor(x => x.TelefoneSecundario)
            .MaximumLength(30)
            .Must((cmd, secundario) => !TelefoneContato.SaoIguais(cmd.TelefonePrincipal, secundario))
            .WithMessage("O telefone secundário deve ser diferente do telefone principal.");
        RuleFor(x => x.TipoDocto).MaximumLength(50);
        RuleFor(x => x.NDocto).MaximumLength(50);
        RuleFor(x => x.Orgao).MaximumLength(50);
        RuleFor(x => x.Cidade).MaximumLength(50);
        RuleFor(x => x.Endereco).MaximumLength(500);
        RuleFor(x => x.Cep)
            .MaximumLength(9)
            .Must(cep => string.IsNullOrWhiteSpace(cep) || CepBrasil.TentarLer(cep, out _, out _))
            .WithMessage("CEP deve ter 8 digitos ou o formato 00000-000.");
        RuleFor(x => x.Uf).MaximumLength(50);
        RuleFor(x => x.Pais).MaximumLength(50);
    }
}

public sealed class CriarUsuarioCommandValidator : AbstractValidator<CriarUsuarioCommand>
{
    public CriarUsuarioCommandValidator()
    {
        RuleFor(x => x.Dados).SetValidator(new DadosUsuarioValidator());
        RuleFor(x => x.Senha).NotEmpty().MinimumLength(8).MaximumLength(200);
    }
}

public sealed class AtualizarUsuarioCommandValidator : AbstractValidator<AtualizarUsuarioCommand>
{
    public AtualizarUsuarioCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Dados).SetValidator(new DadosUsuarioValidator());
        RuleFor(x => x.Senha)
            .MinimumLength(8)
            .MaximumLength(200)
            .When(x => !string.IsNullOrWhiteSpace(x.Senha));
    }
}

public sealed class ExcluirUsuarioCommandValidator : AbstractValidator<ExcluirUsuarioCommand>
{
    public ExcluirUsuarioCommandValidator() => RuleFor(x => x.Id).NotEmpty();
}

public sealed class ObterUsuarioQueryValidator : AbstractValidator<ObterUsuarioQuery>
{
    public ObterUsuarioQueryValidator() => RuleFor(x => x.Id).NotEmpty();
}

public sealed class CriarUsuarioHandler : IRequestHandler<CriarUsuarioCommand, CriarUsuarioResult>
{
    private readonly IUsuarioRepository _usuarios;
    private readonly ICatalogoPermissaoRepository _catalogo;
    private readonly ISenhaHasher _senhas;
    private readonly ILogger<CriarUsuarioHandler> _logger;

    public CriarUsuarioHandler(
        IUsuarioRepository usuarios,
        ICatalogoPermissaoRepository catalogo,
        ISenhaHasher senhas,
        ILogger<CriarUsuarioHandler> logger)
    {
        _usuarios = usuarios;
        _catalogo = catalogo;
        _senhas = senhas;
        _logger = logger;
    }

    public async Task<CriarUsuarioResult> Handle(CriarUsuarioCommand request, CancellationToken cancellationToken)
    {
        try
        {
            if (await _usuarios.EmailEmUsoAsync(request.Dados.EmailPrincipal, null, cancellationToken))
            {
                return CriarUsuarioResult.BadRequest("Ja existe um usuario com este e-mail principal.");
            }

            var usuario = new Usuario(
                request.Dados.Nome,
                request.Dados.EmailPrincipal,
                _senhas.Hash(request.Senha),
                request.Dados.EmailSecundario,
                request.Dados.TelefonePrincipal,
                request.Dados.TelefoneSecundario,
                request.Dados.TipoDocto,
                request.Dados.NDocto,
                request.Dados.DataEmissao,
                request.Dados.Orgao,
                request.Dados.Cidade,
                request.Dados.Endereco,
                request.Dados.Cep,
                request.Dados.Uf,
                request.Dados.Pais,
                request.Dados.SmsAuth,
                request.Dados.EmailAuth);

            if (request.Recursos is { Count: > 0 })
            {
                var recursos = await _catalogo.ObterPorChavesAsync(request.Recursos, cancellationToken);
                var faltando = request.Recursos
                    .Select(c => c.Trim().ToLowerInvariant())
                    .Except(recursos.Select(r => r.Chave), StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (faltando.Count > 0)
                {
                    return CriarUsuarioResult.BadRequest("Recursos desconhecidos: " + string.Join(", ", faltando));
                }

                usuario.SubstituirPermissoes(recursos.Select(r => (r.ModuloId, r.Id)));
            }

            await _usuarios.AddAsync(usuario, cancellationToken);
            await _usuarios.SaveChangesAsync(cancellationToken);
            usuario = await _usuarios.GetByIdComPermissoesAsync(usuario.Id, cancellationToken) ?? usuario;
            _logger.LogInformation("Usuario criado {UsuarioId} {Email}", usuario.Id, usuario.EmailPrincipal);
            return CriarUsuarioResult.Created(usuario);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao criar usuario {Email}", request.Dados.EmailPrincipal);
            return CriarUsuarioResult.Fail(ex.Message);
        }
    }
}

public sealed class AtualizarUsuarioHandler : IRequestHandler<AtualizarUsuarioCommand, AtualizarUsuarioResult>
{
    private readonly IUsuarioRepository _usuarios;
    private readonly ISenhaHasher _senhas;
    private readonly ILogger<AtualizarUsuarioHandler> _logger;

    public AtualizarUsuarioHandler(
        IUsuarioRepository usuarios,
        ISenhaHasher senhas,
        ILogger<AtualizarUsuarioHandler> logger)
    {
        _usuarios = usuarios;
        _senhas = senhas;
        _logger = logger;
    }

    public async Task<AtualizarUsuarioResult> Handle(AtualizarUsuarioCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var usuario = await _usuarios.GetByIdAsync(request.Id, cancellationToken);
            if (usuario is null)
            {
                return AtualizarUsuarioResult.NotFound("Usuario nao encontrado.");
            }

            if (await _usuarios.EmailEmUsoAsync(request.Dados.EmailPrincipal, usuario.Id, cancellationToken))
            {
                return AtualizarUsuarioResult.BadRequest("Ja existe um usuario com este e-mail principal.");
            }

            usuario.Atualizar(
                request.Dados.Nome,
                request.Dados.EmailPrincipal,
                request.Dados.EmailSecundario,
                request.Dados.TelefonePrincipal,
                request.Dados.TelefoneSecundario,
                request.Dados.TipoDocto,
                request.Dados.NDocto,
                request.Dados.DataEmissao,
                request.Dados.Orgao,
                request.Dados.Cidade,
                request.Dados.Endereco,
                request.Dados.Cep,
                request.Dados.Uf,
                request.Dados.Pais,
                request.Dados.SmsAuth,
                request.Dados.EmailAuth);

            if (!string.IsNullOrWhiteSpace(request.Senha))
            {
                usuario.DefinirSenhaHash(_senhas.Hash(request.Senha));
            }

            await _usuarios.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Usuario atualizado {UsuarioId}", usuario.Id);
            return AtualizarUsuarioResult.Ok(usuario);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao atualizar usuario {UsuarioId}", request.Id);
            return AtualizarUsuarioResult.Fail(ex.Message);
        }
    }
}

public sealed class ExcluirUsuarioHandler : IRequestHandler<ExcluirUsuarioCommand, ExcluirUsuarioResult>
{
    private readonly IUsuarioRepository _usuarios;
    private readonly IAdministradorSistemaRepository _admins;
    private readonly ILogger<ExcluirUsuarioHandler> _logger;

    public ExcluirUsuarioHandler(
        IUsuarioRepository usuarios,
        IAdministradorSistemaRepository admins,
        ILogger<ExcluirUsuarioHandler> logger)
    {
        _usuarios = usuarios;
        _admins = admins;
        _logger = logger;
    }

    public async Task<ExcluirUsuarioResult> Handle(ExcluirUsuarioCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var usuario = await _usuarios.GetByIdAsync(request.Id, cancellationToken);
            if (usuario is null)
            {
                return ExcluirUsuarioResult.NotFound("Usuario nao encontrado.");
            }

            if (await _admins.EhAdministradorAsync(usuario.Id, cancellationToken)
                && await _admins.ContarAsync(cancellationToken) <= 1)
            {
                return ExcluirUsuarioResult.BadRequest(
                    "Nao e possivel excluir o ultimo administrador do sistema (ADM_SYS).");
            }

            _usuarios.Remove(usuario);
            await _usuarios.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Usuario excluido {UsuarioId}", request.Id);
            return ExcluirUsuarioResult.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao excluir usuario {UsuarioId}", request.Id);
            return ExcluirUsuarioResult.Fail(ex.Message);
        }
    }
}

public sealed class ObterUsuarioHandler : IRequestHandler<ObterUsuarioQuery, ObterUsuarioResult>
{
    private readonly IUsuarioConsulta _usuarios;
    private readonly ILogger<ObterUsuarioHandler> _logger;

    public ObterUsuarioHandler(IUsuarioConsulta usuarios, ILogger<ObterUsuarioHandler> logger)
    {
        _usuarios = usuarios;
        _logger = logger;
    }

    public async Task<ObterUsuarioResult> Handle(ObterUsuarioQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var usuario = await _usuarios.ObterPorIdAsync(request.Id, incluirPermissoes: true, cancellationToken);
            return usuario is null
                ? ObterUsuarioResult.NotFound("Usuario nao encontrado.")
                : ObterUsuarioResult.Ok(usuario);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao obter usuario {UsuarioId}", request.Id);
            return ObterUsuarioResult.Fail(ex.Message);
        }
    }
}

public sealed class ListarUsuariosHandler : IRequestHandler<ListarUsuariosQuery, ListarUsuariosResult>
{
    private readonly IUsuarioConsulta _usuarios;
    private readonly ILogger<ListarUsuariosHandler> _logger;

    public ListarUsuariosHandler(IUsuarioConsulta usuarios, ILogger<ListarUsuariosHandler> logger)
    {
        _usuarios = usuarios;
        _logger = logger;
    }

    public async Task<ListarUsuariosResult> Handle(ListarUsuariosQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var lista = await _usuarios.ListarAsync(cancellationToken);
            return ListarUsuariosResult.Ok(lista);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao listar usuarios");
            return ListarUsuariosResult.Fail(ex.Message);
        }
    }
}
