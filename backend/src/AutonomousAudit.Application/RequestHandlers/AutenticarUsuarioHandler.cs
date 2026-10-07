using System.Security.Cryptography;
using AutonomousAudit.Application.Data;
using AutonomousAudit.Domain;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AutonomousAudit.Application.RequestHandlers;

public record AutenticarUsuarioCommand(
    string Email,
    string Senha,
    string? SmsAuthCode,
    string? EmailAuthCode) : IRequest<AutenticarUsuarioResult>;

public abstract record AutenticarUsuarioResult
{
    public const string MensagemDesafio =
        "Reenvie o codigo de autenticacao gerado com o usuario e senha novamente.";

    public static AutenticarUsuarioResult Ok(Usuario usuario, TokenJwtEmitido jwt) =>
        new AutenticarUsuarioOk(usuario, jwt);

    public static AutenticarUsuarioResult PedirDesafio(string desafio) =>
        new AutenticarUsuarioDesafio(desafio, MensagemDesafio);

    public static AutenticarUsuarioResult Unauthorized(string message) => new AutenticarUsuarioUnauthorized(message);
    public static AutenticarUsuarioResult Fail(string message) => new AutenticarUsuarioFail(message);
}

public record AutenticarUsuarioOk(Usuario Usuario, TokenJwtEmitido Jwt) : AutenticarUsuarioResult;
public record AutenticarUsuarioDesafio(string Desafio, string Mensagem) : AutenticarUsuarioResult;
public record AutenticarUsuarioUnauthorized(string Message) : AutenticarUsuarioResult;
public record AutenticarUsuarioFail(string Message) : AutenticarUsuarioResult;

public sealed class AutenticarUsuarioCommandValidator : AbstractValidator<AutenticarUsuarioCommand>
{
    public AutenticarUsuarioCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Senha).NotEmpty();
    }
}

public sealed class AutenticarUsuarioHandler : IRequestHandler<AutenticarUsuarioCommand, AutenticarUsuarioResult>
{
    private readonly IUsuarioRepository _usuarios;
    private readonly ISenhaHasher _senhas;
    private readonly IJwtTokenService _jwt;
    private readonly IAuthDesafioPublisher _desafios;
    private readonly ITitanSmtpSettings _titan;
    private readonly ILogger<AutenticarUsuarioHandler> _logger;

    public AutenticarUsuarioHandler(
        IUsuarioRepository usuarios,
        ISenhaHasher senhas,
        IJwtTokenService jwt,
        IAuthDesafioPublisher desafios,
        ITitanSmtpSettings titan,
        ILogger<AutenticarUsuarioHandler> logger)
    {
        _usuarios = usuarios;
        _senhas = senhas;
        _jwt = jwt;
        _desafios = desafios;
        _titan = titan;
        _logger = logger;
    }

    public async Task<AutenticarUsuarioResult> Handle(AutenticarUsuarioCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var usuario = await _usuarios.GetByEmailAsync(request.Email, cancellationToken);
            if (usuario is null || !_senhas.Verificar(usuario.SenhaHash, request.Senha))
            {
                _logger.LogWarning("Login recusado para {Email}", request.Email.Trim().ToLowerInvariant());
                return AutenticarUsuarioResult.Unauthorized("E-mail ou senha invalidos.");
            }

            var smsInformado = !string.IsNullOrWhiteSpace(request.SmsAuthCode);
            var emailInformado = !string.IsNullOrWhiteSpace(request.EmailAuthCode);
            var smsOk = !usuario.SmsAuth || usuario.CodigoSmsConfere(request.SmsAuthCode);
            var emailOk = !usuario.EmailAuth || usuario.CodigoEmailConfere(request.EmailAuthCode);

            if (smsOk && emailOk)
            {
                usuario.LimparCodigosAuth();
                await _usuarios.SaveChangesAsync(cancellationToken);
                var token = _jwt.Emitir(usuario);
                _logger.LogInformation("Login OK usuario={UsuarioId} expira={ExpiraEm}", usuario.Id, token.ExpiraEm);
                return AutenticarUsuarioResult.Ok(usuario, token);
            }

            if ((usuario.SmsAuth && smsInformado && !smsOk)
                || (usuario.EmailAuth && emailInformado && !emailOk))
            {
                _logger.LogWarning("Codigo de autenticacao invalido usuario={UsuarioId}", usuario.Id);
                return AutenticarUsuarioResult.Unauthorized("Codigo de autenticacao invalido.");
            }

            var desafios = new List<string>();
            string? smsNumero = null;
            string? smsMensagem = null;
            string? emailMensagem = null;
            if (usuario.SmsAuth && !smsOk)
            {
                var telefone = usuario.TelefonePrincipal ?? usuario.TelefoneSecundario;
                if (string.IsNullOrWhiteSpace(telefone))
                {
                    return AutenticarUsuarioResult.Fail("Usuario sem telefone para autenticacao SMS.");
                }

                var codigo = GerarCodigo();
                usuario.DefinirCodigoSms(codigo);
                smsNumero = SmsNumero.Normalizar(telefone);
                smsMensagem = $"Codigo de autenticacao no sistema: {codigo}";
                desafios.Add("sms");
            }

            if (usuario.EmailAuth && !emailOk)
            {
                if (!_titan.EstaConfigurado)
                {
                    return AutenticarUsuarioResult.Fail("SMTP Titan nao configurado para autenticacao por e-mail.");
                }

                var codigo = GerarCodigo();
                usuario.DefinirCodigoEmail(codigo);
                emailMensagem = $"Codigo de autenticacao no sistema: {codigo}";
                desafios.Add("email");
            }

            await _usuarios.SaveChangesAsync(cancellationToken);
            if (smsNumero is not null && smsMensagem is not null)
            {
                await _desafios.EnfileirarSmsAsync(smsNumero, smsMensagem, usuario.Id.ToString("N"));
            }

            if (emailMensagem is not null)
            {
                await _desafios.EnfileirarEmailAsync(
                    [usuario.EmailPrincipal],
                    "Codigo de autenticacao no sistema",
                    emailMensagem);
            }
            var desafio = string.Join("_", desafios);
            _logger.LogInformation(
                "Login em desafio usuario={UsuarioId} desafio={Desafio}",
                usuario.Id,
                desafio);
            return AutenticarUsuarioResult.PedirDesafio(string.IsNullOrWhiteSpace(desafio) ? "sms" : desafio);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao autenticar {Email}", request.Email);
            return AutenticarUsuarioResult.Fail(ex.Message);
        }
    }

    private static string GerarCodigo() =>
        RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
}
