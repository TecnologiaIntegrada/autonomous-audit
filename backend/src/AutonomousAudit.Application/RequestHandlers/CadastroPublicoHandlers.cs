using AutonomousAudit.Application;
using AutonomousAudit.Application.Data;
using AutonomousAudit.Application.Security;
using AutonomousAudit.Domain;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AutonomousAudit.Application.RequestHandlers;

public record CadastrarUsuarioPublicoCommand(
    string Nome,
    string Email,
    string EmailConfirmacao,
    string TelefonePrincipal,
    string Senha,
    string SenhaConfirmacao,
    string Cep,
    string Endereco,
    string Numero,
    string Bairro,
    string Cidade,
    string Uf,
    string Pais) : IRequest<CadastrarUsuarioPublicoResult>;

public abstract record CadastrarUsuarioPublicoResult
{
    public static CadastrarUsuarioPublicoResult Ok(TokenJwtEmitido jwt) => new CadastrarUsuarioPublicoOk(jwt);
    public static CadastrarUsuarioPublicoResult BadRequest(string message) => new CadastrarUsuarioPublicoBadRequest(message);
    public static CadastrarUsuarioPublicoResult Fail(string message) => new CadastrarUsuarioPublicoFail(message);
}

public record CadastrarUsuarioPublicoOk(TokenJwtEmitido Jwt) : CadastrarUsuarioPublicoResult;
public record CadastrarUsuarioPublicoBadRequest(string Message) : CadastrarUsuarioPublicoResult;
public record CadastrarUsuarioPublicoFail(string Message) : CadastrarUsuarioPublicoResult;

public sealed class CadastrarUsuarioPublicoCommandValidator : AbstractValidator<CadastrarUsuarioPublicoCommand>
{
    public CadastrarUsuarioPublicoCommandValidator()
    {
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(320);
        RuleFor(x => x.EmailConfirmacao)
            .Must((cmd, confirmacao) =>
                string.Equals(
                    (confirmacao ?? string.Empty).Trim(),
                    (cmd.Email ?? string.Empty).Trim(),
                    StringComparison.OrdinalIgnoreCase))
            .WithMessage("A confirmacao do e-mail nao confere.");
        RuleFor(x => x.TelefonePrincipal).NotEmpty().MaximumLength(30);
        RuleFor(x => x.Senha).NotEmpty().MinimumLength(8).MaximumLength(200);
        RuleFor(x => x.SenhaConfirmacao).Equal(x => x.Senha).WithMessage("A confirmacao da senha nao confere.");
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

public sealed class CadastrarUsuarioPublicoHandler : IRequestHandler<CadastrarUsuarioPublicoCommand, CadastrarUsuarioPublicoResult>
{
    private readonly IUsuarioRepository _usuarios;
    private readonly ICatalogoPermissaoRepository _catalogo;
    private readonly ISenhaHasher _senhas;
    private readonly IJwtTokenService _jwt;
    private readonly ILogger<CadastrarUsuarioPublicoHandler> _logger;

    public CadastrarUsuarioPublicoHandler(
        IUsuarioRepository usuarios,
        ICatalogoPermissaoRepository catalogo,
        ISenhaHasher senhas,
        IJwtTokenService jwt,
        ILogger<CadastrarUsuarioPublicoHandler> logger)
    {
        _usuarios = usuarios;
        _catalogo = catalogo;
        _senhas = senhas;
        _jwt = jwt;
        _logger = logger;
    }

    public async Task<CadastrarUsuarioPublicoResult> Handle(
        CadastrarUsuarioPublicoCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            var email = request.Email.Trim().ToLowerInvariant();
            if (await _usuarios.EmailEmUsoAsync(email, null, cancellationToken))
            {
                return CadastrarUsuarioPublicoResult.BadRequest(
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
                email,
                _senhas.Hash(request.Senha),
                telefonePrincipal: request.TelefonePrincipal,
                cidade: request.Cidade,
                endereco: endereco,
                cep: cep,
                uf: request.Uf,
                pais: request.Pais);

            var chaves = CatalogoRecursos.Recursos.Select(r => r.Chave).ToList();
            var recursos = await _catalogo.ObterPorChavesAsync(chaves, cancellationToken);
            usuario.SubstituirPermissoes(recursos.Select(r => (r.ModuloId, r.Id)));

            await _usuarios.AddAsync(usuario, cancellationToken);
            await _usuarios.SaveChangesAsync(cancellationToken);
            usuario = await _usuarios.GetByIdComPermissoesAsync(usuario.Id, cancellationToken) ?? usuario;
            _logger.LogInformation("Usuario cadastrado {UsuarioId} {Email}", usuario.Id, usuario.EmailPrincipal);
            return CadastrarUsuarioPublicoResult.Ok(_jwt.Emitir(usuario));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha no cadastro publico {Email}", request.Email);
            return CadastrarUsuarioPublicoResult.Fail(ex.Message);
        }
    }
}
