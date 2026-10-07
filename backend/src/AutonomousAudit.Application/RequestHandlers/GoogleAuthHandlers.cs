using System.Security.Cryptography;
using AutonomousAudit.Application;
using AutonomousAudit.Application.Data;
using AutonomousAudit.Application.Security;
using AutonomousAudit.Domain;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AutonomousAudit.Application.RequestHandlers;

public record AutenticarGoogleCommand(string IdToken) : IRequest<AutenticarGoogleResult>;

public abstract record AutenticarGoogleResult
{
    public static AutenticarGoogleResult Pronto(TokenJwtEmitido jwt) => new AutenticarGooglePronto(jwt);
    public static AutenticarGoogleResult CadastroPendente(
        TokenJwtEmitido registro,
        string email,
        string nome,
        string? foto) => new AutenticarGoogleCadastroPendente(registro, email, nome, foto);
    public static AutenticarGoogleResult EmailEmUso(string message) => new AutenticarGoogleEmailEmUso(message);
    public static AutenticarGoogleResult Unauthorized(string message) => new AutenticarGoogleUnauthorized(message);
    public static AutenticarGoogleResult Fail(string message) => new AutenticarGoogleFail(message);
}

public record AutenticarGooglePronto(TokenJwtEmitido Jwt) : AutenticarGoogleResult;
public record AutenticarGoogleCadastroPendente(TokenJwtEmitido Registro, string Email, string Nome, string? Foto) : AutenticarGoogleResult;
public record AutenticarGoogleEmailEmUso(string Message) : AutenticarGoogleResult;
public record AutenticarGoogleUnauthorized(string Message) : AutenticarGoogleResult;
public record AutenticarGoogleFail(string Message) : AutenticarGoogleResult;

public sealed class AutenticarGoogleCommandValidator : AbstractValidator<AutenticarGoogleCommand>
{
    public AutenticarGoogleCommandValidator() =>
        RuleFor(x => x.IdToken).NotEmpty();
}

public sealed class AutenticarGoogleHandler : IRequestHandler<AutenticarGoogleCommand, AutenticarGoogleResult>
{
    private readonly IGoogleIdTokenValidator _google;
    private readonly IUsuarioRepository _usuarios;
    private readonly IJwtTokenService _jwt;
    private readonly ILogger<AutenticarGoogleHandler> _logger;

    public AutenticarGoogleHandler(
        IGoogleIdTokenValidator google,
        IUsuarioRepository usuarios,
        IJwtTokenService jwt,
        ILogger<AutenticarGoogleHandler> logger)
    {
        _google = google;
        _usuarios = usuarios;
        _jwt = jwt;
        _logger = logger;
    }

    public async Task<AutenticarGoogleResult> Handle(AutenticarGoogleCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var identidade = await _google.ValidarAsync(request.IdToken, cancellationToken);
            if (identidade is null)
            {
                return AutenticarGoogleResult.Unauthorized("Token Google invalido ou expirado.");
            }

            if (!identidade.EmailVerificado)
            {
                return AutenticarGoogleResult.Unauthorized("O e-mail da conta Google nao esta verificado.");
            }

            var porSub = await _usuarios.GetByGoogleSubAsync(identidade.Sub, cancellationToken);
            if (porSub is not null)
            {
                porSub.VincularGoogle(identidade.Sub, identidade.Picture);
                await _usuarios.SaveChangesAsync(cancellationToken);
                var jwt = _jwt.Emitir(porSub);
                _logger.LogInformation("Login Google OK usuario={UsuarioId}", porSub.Id);
                return AutenticarGoogleResult.Pronto(jwt);
            }

            if (await _usuarios.EmailEmUsoAsync(identidade.Email, null, cancellationToken))
            {
                return AutenticarGoogleResult.EmailEmUso(
                    "Este e-mail ja possui cadastro. Entre com e-mail e senha.");
            }

            var registro = _jwt.EmitirCadastroGoogle(
                new CadastroGoogleToken(identidade.Sub, identidade.Email, identidade.Nome, identidade.Picture));
            return AutenticarGoogleResult.CadastroPendente(
                registro, identidade.Email, identidade.Nome, identidade.Picture);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha no login Google");
            return AutenticarGoogleResult.Fail(ex.Message);
        }
    }
}

public record CompletarCadastroGoogleCommand(
    string RegistroToken,
    string Nome,
    string Senha,
    string SenhaConfirmacao,
    string EmailConfirmacao,
    string TelefonePrincipal,
    string Cep,
    string Endereco,
    string Numero,
    string Bairro,
    string Cidade,
    string Uf,
    string Pais) : IRequest<CompletarCadastroGoogleResult>;

public abstract record CompletarCadastroGoogleResult
{
    public static CompletarCadastroGoogleResult Ok(TokenJwtEmitido jwt) => new CompletarCadastroGoogleOk(jwt);
    public static CompletarCadastroGoogleResult BadRequest(string message) => new CompletarCadastroGoogleBadRequest(message);
    public static CompletarCadastroGoogleResult Unauthorized(string message) => new CompletarCadastroGoogleUnauthorized(message);
    public static CompletarCadastroGoogleResult Fail(string message) => new CompletarCadastroGoogleFail(message);
}

public record CompletarCadastroGoogleOk(TokenJwtEmitido Jwt) : CompletarCadastroGoogleResult;
public record CompletarCadastroGoogleBadRequest(string Message) : CompletarCadastroGoogleResult;
public record CompletarCadastroGoogleUnauthorized(string Message) : CompletarCadastroGoogleResult;
public record CompletarCadastroGoogleFail(string Message) : CompletarCadastroGoogleResult;

public sealed class CompletarCadastroGoogleCommandValidator : AbstractValidator<CompletarCadastroGoogleCommand>
{
    public CompletarCadastroGoogleCommandValidator()
    {
        RuleFor(x => x.RegistroToken).NotEmpty();
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Senha).NotEmpty().MinimumLength(8).MaximumLength(200);
        RuleFor(x => x.SenhaConfirmacao).Equal(x => x.Senha).WithMessage("A confirmacao da senha nao confere.");
        RuleFor(x => x.EmailConfirmacao).NotEmpty().EmailAddress();
        RuleFor(x => x.TelefonePrincipal).NotEmpty().MaximumLength(30);
        RuleFor(x => x.Cep)
            .NotEmpty()
            .Must(cep => CepBrasil.TentarLer(cep, out _, out _))
            .WithMessage("CEP invalido.");
        RuleFor(x => x.Endereco).NotEmpty().MaximumLength(400);
        RuleFor(x => x.Numero).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Bairro).MaximumLength(80);
        RuleFor(x => x.Cidade).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Uf)
            .NotEmpty()
            .Must(uf => CepBrasil.Ufs.Contains((uf ?? string.Empty).Trim()))
            .WithMessage("UF invalida.");
        RuleFor(x => x.Pais).NotEmpty().MaximumLength(50);
    }
}

public sealed class CompletarCadastroGoogleHandler : IRequestHandler<CompletarCadastroGoogleCommand, CompletarCadastroGoogleResult>
{
    private readonly IJwtTokenService _jwt;
    private readonly IUsuarioRepository _usuarios;
    private readonly ICatalogoPermissaoRepository _catalogo;
    private readonly ISenhaHasher _senhas;
    private readonly ILogger<CompletarCadastroGoogleHandler> _logger;

    public CompletarCadastroGoogleHandler(
        IJwtTokenService jwt,
        IUsuarioRepository usuarios,
        ICatalogoPermissaoRepository catalogo,
        ISenhaHasher senhas,
        ILogger<CompletarCadastroGoogleHandler> logger)
    {
        _jwt = jwt;
        _usuarios = usuarios;
        _catalogo = catalogo;
        _senhas = senhas;
        _logger = logger;
    }

    public async Task<CompletarCadastroGoogleResult> Handle(
        CompletarCadastroGoogleCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            var registro = _jwt.LerCadastroGoogle(request.RegistroToken);
            if (registro is null)
            {
                return CompletarCadastroGoogleResult.Unauthorized(
                    "Sessao de cadastro expirada. Entre de novo com o Google.");
            }

            var emailConfirmado = request.EmailConfirmacao.Trim().ToLowerInvariant();
            if (!string.Equals(emailConfirmado, registro.Email, StringComparison.OrdinalIgnoreCase))
            {
                return CompletarCadastroGoogleResult.BadRequest(
                    "O e-mail de confirmacao deve ser o mesmo da conta Google.");
            }

            if (await _usuarios.GetByGoogleSubAsync(registro.Sub, cancellationToken) is not null)
            {
                var existente = await _usuarios.GetByGoogleSubAsync(registro.Sub, cancellationToken);
                return CompletarCadastroGoogleResult.Ok(_jwt.Emitir(existente!));
            }

            if (await _usuarios.EmailEmUsoAsync(registro.Email, null, cancellationToken))
            {
                return CompletarCadastroGoogleResult.BadRequest(
                    "Este e-mail ja possui cadastro. Entre com e-mail e senha.");
            }

            var cep = CepBrasil.Formatado(request.Cep);
            var logradouro = request.Endereco.Trim();
            var numero = request.Numero.Trim();
            var bairro = (request.Bairro ?? string.Empty).Trim();
            var endereco = string.IsNullOrWhiteSpace(bairro)
                ? $"{logradouro}, {numero}"
                : $"{logradouro}, {numero} - {bairro}";
            if (endereco.Length > 500)
            {
                endereco = endereco[..500];
            }

            var usuario = new Usuario(
                request.Nome,
                registro.Email,
                _senhas.Hash(request.Senha),
                telefonePrincipal: request.TelefonePrincipal,
                cidade: request.Cidade,
                endereco: endereco,
                cep: cep,
                uf: request.Uf,
                pais: request.Pais);
            usuario.VincularGoogle(registro.Sub, registro.Picture);

            var chaves = CatalogoRecursos.Recursos.Select(r => r.Chave).ToList();
            var recursos = await _catalogo.ObterPorChavesAsync(chaves, cancellationToken);
            usuario.SubstituirPermissoes(recursos.Select(r => (r.ModuloId, r.Id)));

            await _usuarios.AddAsync(usuario, cancellationToken);
            await _usuarios.SaveChangesAsync(cancellationToken);
            usuario = await _usuarios.GetByIdComPermissoesAsync(usuario.Id, cancellationToken) ?? usuario;
            _logger.LogInformation("Usuario Google criado {UsuarioId} {Email}", usuario.Id, usuario.EmailPrincipal);
            return CompletarCadastroGoogleResult.Ok(_jwt.Emitir(usuario));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao completar cadastro Google");
            return CompletarCadastroGoogleResult.Fail(ex.Message);
        }
    }
}

public record SolicitarRecuperacaoSenhaCommand(string Email) : IRequest<SolicitarRecuperacaoSenhaResult>;

public abstract record SolicitarRecuperacaoSenhaResult
{
    public static SolicitarRecuperacaoSenhaResult Ok() => new SolicitarRecuperacaoSenhaOk();
    public static SolicitarRecuperacaoSenhaResult Fail(string message) => new SolicitarRecuperacaoSenhaFail(message);
}

public record SolicitarRecuperacaoSenhaOk : SolicitarRecuperacaoSenhaResult;
public record SolicitarRecuperacaoSenhaFail(string Message) : SolicitarRecuperacaoSenhaResult;

public sealed class SolicitarRecuperacaoSenhaCommandValidator : AbstractValidator<SolicitarRecuperacaoSenhaCommand>
{
    public SolicitarRecuperacaoSenhaCommandValidator() =>
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
}

public sealed class SolicitarRecuperacaoSenhaHandler : IRequestHandler<SolicitarRecuperacaoSenhaCommand, SolicitarRecuperacaoSenhaResult>
{
    private readonly IUsuarioRepository _usuarios;
    private readonly IAuthDesafioPublisher _emails;
    private readonly IFrontUrl _front;
    private readonly ILogger<SolicitarRecuperacaoSenhaHandler> _logger;

    public SolicitarRecuperacaoSenhaHandler(
        IUsuarioRepository usuarios,
        IAuthDesafioPublisher emails,
        IFrontUrl front,
        ILogger<SolicitarRecuperacaoSenhaHandler> logger)
    {
        _usuarios = usuarios;
        _emails = emails;
        _front = front;
        _logger = logger;
    }

    public async Task<SolicitarRecuperacaoSenhaResult> Handle(
        SolicitarRecuperacaoSenhaCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            var usuario = await _usuarios.GetByEmailAsync(request.Email, cancellationToken);
            if (usuario is null)
            {
                return SolicitarRecuperacaoSenhaResult.Ok();
            }

            var codigo = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
            usuario.DefinirResetSenha(codigo, DateTimeOffset.UtcNow.AddMinutes(15));
            await _usuarios.SaveChangesAsync(cancellationToken);

            var link = $"{_front.BaseUrl}/redefinir-senha?email={Uri.EscapeDataString(usuario.EmailPrincipal)}";
            var corpo =
                $"Codigo de recuperacao de senha: {codigo}\n" +
                "O codigo vale 15 minutos.\n" +
                $"Abra {link} e informe o codigo junto com a nova senha.";
            await _emails.EnfileirarEmailAsync(
                [usuario.EmailPrincipal],
                "Recuperacao de senha — Autonomous Audit",
                corpo);
            _logger.LogInformation("Recuperacao de senha enfileirada usuario={UsuarioId}", usuario.Id);
            return SolicitarRecuperacaoSenhaResult.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao solicitar recuperacao de senha");
            return SolicitarRecuperacaoSenhaResult.Fail(ex.Message);
        }
    }
}

public record RedefinirSenhaCommand(
    string Email,
    string Codigo,
    string Senha,
    string SenhaConfirmacao) : IRequest<RedefinirSenhaResult>;

public abstract record RedefinirSenhaResult
{
    public static RedefinirSenhaResult Ok(TokenJwtEmitido jwt) => new RedefinirSenhaOk(jwt);
    public static RedefinirSenhaResult BadRequest(string message) => new RedefinirSenhaBadRequest(message);
    public static RedefinirSenhaResult Fail(string message) => new RedefinirSenhaFail(message);
}

public record RedefinirSenhaOk(TokenJwtEmitido Jwt) : RedefinirSenhaResult;
public record RedefinirSenhaBadRequest(string Message) : RedefinirSenhaResult;
public record RedefinirSenhaFail(string Message) : RedefinirSenhaResult;

public sealed class RedefinirSenhaCommandValidator : AbstractValidator<RedefinirSenhaCommand>
{
    public RedefinirSenhaCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Codigo).NotEmpty().Length(6);
        RuleFor(x => x.Senha).NotEmpty().MinimumLength(8).MaximumLength(200);
        RuleFor(x => x.SenhaConfirmacao).Equal(x => x.Senha).WithMessage("A confirmacao da senha nao confere.");
    }
}

public sealed class RedefinirSenhaHandler : IRequestHandler<RedefinirSenhaCommand, RedefinirSenhaResult>
{
    private readonly IUsuarioRepository _usuarios;
    private readonly ISenhaHasher _senhas;
    private readonly IJwtTokenService _jwt;
    private readonly ILogger<RedefinirSenhaHandler> _logger;

    public RedefinirSenhaHandler(
        IUsuarioRepository usuarios,
        ISenhaHasher senhas,
        IJwtTokenService jwt,
        ILogger<RedefinirSenhaHandler> logger)
    {
        _usuarios = usuarios;
        _senhas = senhas;
        _jwt = jwt;
        _logger = logger;
    }

    public async Task<RedefinirSenhaResult> Handle(RedefinirSenhaCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var usuario = await _usuarios.GetByEmailAsync(request.Email, cancellationToken);
            if (usuario is null || !usuario.ResetSenhaConfere(request.Codigo))
            {
                return RedefinirSenhaResult.BadRequest("Codigo invalido ou expirado.");
            }

            usuario.DefinirSenhaHash(_senhas.Hash(request.Senha));
            usuario.LimparResetSenha();
            await _usuarios.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Senha redefinida usuario={UsuarioId}", usuario.Id);
            return RedefinirSenhaResult.Ok(_jwt.Emitir(usuario));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao redefinir senha");
            return RedefinirSenhaResult.Fail(ex.Message);
        }
    }
}
