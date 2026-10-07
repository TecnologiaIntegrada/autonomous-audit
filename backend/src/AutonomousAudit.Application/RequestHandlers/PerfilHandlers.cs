using System.Security.Cryptography;
using AutonomousAudit.Application;
using AutonomousAudit.Application.Data;
using AutonomousAudit.Domain;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AutonomousAudit.Application.RequestHandlers;

public static class CanalContato
{
    public const string Principal = "principal";
    public const string Secundario = "secundario";

    public static bool EhValido(string? canal) =>
        string.Equals(canal, Principal, StringComparison.OrdinalIgnoreCase)
        || string.Equals(canal, Secundario, StringComparison.OrdinalIgnoreCase);

    public static string Normalizar(string canal) =>
        string.Equals(canal, Secundario, StringComparison.OrdinalIgnoreCase) ? Secundario : Principal;
}

public record AtualizarMeuPerfilCommand(
    Guid UsuarioId,
    string Nome,
    string? EmailSecundario,
    string? TelefonePrincipal,
    string? TelefoneSecundario,
    string? Cidade,
    string? Endereco,
    string? Cep,
    string? Uf,
    string? Pais) : ICommand<AtualizarUsuarioResult>;

public sealed class AtualizarMeuPerfilCommandValidator : AbstractValidator<AtualizarMeuPerfilCommand>
{
    public AtualizarMeuPerfilCommandValidator()
    {
        RuleFor(x => x.UsuarioId).NotEmpty();
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(200);
        RuleFor(x => x.EmailSecundario).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.EmailSecundario));
        RuleFor(x => x.TelefonePrincipal).MaximumLength(30);
        RuleFor(x => x.TelefoneSecundario)
            .MaximumLength(30)
            .Must((cmd, secundario) => !TelefoneContato.SaoIguais(cmd.TelefonePrincipal, secundario))
            .WithMessage("O telefone secundário deve ser diferente do telefone principal.");
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

public sealed class AtualizarMeuPerfilHandler : IRequestHandler<AtualizarMeuPerfilCommand, AtualizarUsuarioResult>
{
    private readonly IUsuarioRepository _usuarios;
    private readonly ILogger<AtualizarMeuPerfilHandler> _logger;

    public AtualizarMeuPerfilHandler(IUsuarioRepository usuarios, ILogger<AtualizarMeuPerfilHandler> logger)
    {
        _usuarios = usuarios;
        _logger = logger;
    }

    public async Task<AtualizarUsuarioResult> Handle(AtualizarMeuPerfilCommand request, CancellationToken cancellationToken)
    {
        var usuario = await _usuarios.GetByIdComPermissoesAsync(request.UsuarioId, cancellationToken);
        if (usuario is null)
        {
            return AtualizarUsuarioResult.NotFound("Usuario nao encontrado.");
        }

        usuario.AtualizarPerfil(
            request.Nome,
            request.EmailSecundario,
            request.TelefonePrincipal,
            request.TelefoneSecundario,
            request.Cidade,
            request.Endereco,
            request.Cep,
            request.Uf,
            request.Pais);
        await _usuarios.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Perfil atualizado {UsuarioId}", usuario.Id);
        return AtualizarUsuarioResult.Ok(usuario);
    }
}

public record SolicitarVerificacaoContatoCommand(Guid UsuarioId, string Tipo, string Canal)
    : ICommand<SolicitarVerificacaoContatoResult>;

public abstract record SolicitarVerificacaoContatoResult
{
    public static SolicitarVerificacaoContatoResult Ok(string mensagem) => new SolicitarVerificacaoContatoOk(mensagem);
    public static SolicitarVerificacaoContatoResult BadRequest(string message) => new SolicitarVerificacaoContatoBadRequest(message);
    public static SolicitarVerificacaoContatoResult Fail(string message) => new SolicitarVerificacaoContatoFail(message);
}

public record SolicitarVerificacaoContatoOk(string Mensagem) : SolicitarVerificacaoContatoResult;
public record SolicitarVerificacaoContatoBadRequest(string Message) : SolicitarVerificacaoContatoResult;
public record SolicitarVerificacaoContatoFail(string Message) : SolicitarVerificacaoContatoResult;

public sealed class SolicitarVerificacaoContatoHandler
    : IRequestHandler<SolicitarVerificacaoContatoCommand, SolicitarVerificacaoContatoResult>
{
    private readonly IUsuarioRepository _usuarios;
    private readonly IAuthDesafioPublisher _desafios;
    private readonly ITitanSmtpSettings _titan;
    private readonly ILogger<SolicitarVerificacaoContatoHandler> _logger;

    public SolicitarVerificacaoContatoHandler(
        IUsuarioRepository usuarios,
        IAuthDesafioPublisher desafios,
        ITitanSmtpSettings titan,
        ILogger<SolicitarVerificacaoContatoHandler> logger)
    {
        _usuarios = usuarios;
        _desafios = desafios;
        _titan = titan;
        _logger = logger;
    }

    public async Task<SolicitarVerificacaoContatoResult> Handle(
        SolicitarVerificacaoContatoCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!CanalContato.EhValido(request.Canal))
            {
                return SolicitarVerificacaoContatoResult.BadRequest("Informe o canal principal ou secundario.");
            }

            var usuario = await _usuarios.GetByIdAsync(request.UsuarioId, cancellationToken);
            if (usuario is null)
            {
                return SolicitarVerificacaoContatoResult.BadRequest("Usuario nao encontrado.");
            }

            var canal = CanalContato.Normalizar(request.Canal);
            var codigo = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
            var expira = DateTimeOffset.UtcNow.AddMinutes(15);
            var tipo = (request.Tipo ?? string.Empty).Trim().ToLowerInvariant();

            if (tipo == "email")
            {
                var destino = canal == CanalContato.Principal ? usuario.EmailPrincipal : usuario.EmailSecundario;
                if (string.IsNullOrWhiteSpace(destino))
                {
                    return SolicitarVerificacaoContatoResult.BadRequest("Informe o e-mail antes de verificar.");
                }

                if (!_titan.EstaConfigurado)
                {
                    return SolicitarVerificacaoContatoResult.Fail("SMTP Titan nao configurado para envio de e-mail.");
                }

                usuario.IniciarVerificacaoEmail(canal, codigo, expira);
                await _usuarios.SaveChangesAsync(cancellationToken);
                await _desafios.EnfileirarEmailAsync(
                    [destino],
                    "Codigo de verificacao de e-mail",
                    $"Codigo de verificacao no Autonomous Audit: {codigo}");
                return SolicitarVerificacaoContatoResult.Ok("Enviamos um codigo de 6 digitos para o e-mail.");
            }

            if (tipo == "sms")
            {
                var telefone = canal == CanalContato.Principal ? usuario.TelefonePrincipal : usuario.TelefoneSecundario;
                if (string.IsNullOrWhiteSpace(telefone))
                {
                    return SolicitarVerificacaoContatoResult.BadRequest("Informe o telefone antes de verificar.");
                }

                if (canal == CanalContato.Secundario
                    && TelefoneContato.SaoIguais(usuario.TelefonePrincipal, usuario.TelefoneSecundario))
                {
                    return SolicitarVerificacaoContatoResult.BadRequest(
                        "O telefone secundário deve ser diferente do telefone principal.");
                }

                usuario.IniciarVerificacaoSms(canal, codigo, expira);
                await _usuarios.SaveChangesAsync(cancellationToken);
                await _desafios.EnfileirarSmsAsync(
                    SmsNumero.Normalizar(telefone),
                    $"Codigo de verificacao no Autonomous Audit: {codigo}",
                    usuario.Id.ToString("N"));
                return SolicitarVerificacaoContatoResult.Ok("Enviamos um codigo de 6 digitos por SMS.");
            }

            return SolicitarVerificacaoContatoResult.BadRequest("Tipo de verificacao invalido.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao solicitar verificacao usuario={UsuarioId}", request.UsuarioId);
            return SolicitarVerificacaoContatoResult.Fail(ex.Message);
        }
    }
}

public record ConfirmarVerificacaoContatoCommand(Guid UsuarioId, string Tipo, string Canal, string Codigo)
    : ICommand<ConfirmarVerificacaoContatoResult>;

public abstract record ConfirmarVerificacaoContatoResult
{
    public static ConfirmarVerificacaoContatoResult Ok(Usuario usuario) => new ConfirmarVerificacaoContatoOk(usuario);
    public static ConfirmarVerificacaoContatoResult BadRequest(string message) => new ConfirmarVerificacaoContatoBadRequest(message);
}

public record ConfirmarVerificacaoContatoOk(Usuario Usuario) : ConfirmarVerificacaoContatoResult;
public record ConfirmarVerificacaoContatoBadRequest(string Message) : ConfirmarVerificacaoContatoResult;

public sealed class ConfirmarVerificacaoContatoHandler
    : IRequestHandler<ConfirmarVerificacaoContatoCommand, ConfirmarVerificacaoContatoResult>
{
    private readonly IUsuarioRepository _usuarios;

    public ConfirmarVerificacaoContatoHandler(IUsuarioRepository usuarios)
    {
        _usuarios = usuarios;
    }

    public async Task<ConfirmarVerificacaoContatoResult> Handle(
        ConfirmarVerificacaoContatoCommand request,
        CancellationToken cancellationToken)
    {
        if (!CanalContato.EhValido(request.Canal))
        {
            return ConfirmarVerificacaoContatoResult.BadRequest("Informe o canal principal ou secundario.");
        }

        var usuario = await _usuarios.GetByIdComPermissoesAsync(request.UsuarioId, cancellationToken);
        if (usuario is null)
        {
            return ConfirmarVerificacaoContatoResult.BadRequest("Usuario nao encontrado.");
        }

        var canal = CanalContato.Normalizar(request.Canal);
        var tipo = (request.Tipo ?? string.Empty).Trim().ToLowerInvariant();
        var ok = tipo switch
        {
            "email" => usuario.ConfirmarVerificacaoEmail(canal, request.Codigo),
            "sms" => usuario.ConfirmarVerificacaoSms(canal, request.Codigo),
            _ => false
        };
        if (!ok)
        {
            return ConfirmarVerificacaoContatoResult.BadRequest("Codigo invalido ou expirado.");
        }

        await _usuarios.SaveChangesAsync(cancellationToken);
        return ConfirmarVerificacaoContatoResult.Ok(usuario);
    }
}

public record EmitirTokenRelatorioCommand(Guid UsuarioId) : ICommand<EmitirTokenRelatorioResult>;

public abstract record EmitirTokenRelatorioResult
{
    public static EmitirTokenRelatorioResult Ok(string token, DateTimeOffset expiraEm) =>
        new EmitirTokenRelatorioOk(token, expiraEm);

    public static EmitirTokenRelatorioResult NotFound() => new EmitirTokenRelatorioNotFound();
}

public record EmitirTokenRelatorioOk(string Token, DateTimeOffset ExpiraEm) : EmitirTokenRelatorioResult;
public record EmitirTokenRelatorioNotFound : EmitirTokenRelatorioResult;

public sealed class EmitirTokenRelatorioHandler : IRequestHandler<EmitirTokenRelatorioCommand, EmitirTokenRelatorioResult>
{
    private readonly IUsuarioRepository _usuarios;
    private readonly IJwtTokenService _jwt;

    public EmitirTokenRelatorioHandler(IUsuarioRepository usuarios, IJwtTokenService jwt)
    {
        _usuarios = usuarios;
        _jwt = jwt;
    }

    public async Task<EmitirTokenRelatorioResult> Handle(
        EmitirTokenRelatorioCommand request,
        CancellationToken cancellationToken)
    {
        var usuario = await _usuarios.GetByIdAsync(request.UsuarioId, cancellationToken);
        if (usuario is null)
        {
            return EmitirTokenRelatorioResult.NotFound();
        }

        var emitido = _jwt.EmitirRelatorio(usuario);
        var jti = _jwt.LerIdentidade(emitido.Token)?.Jti;
        if (string.IsNullOrWhiteSpace(jti))
        {
            return EmitirTokenRelatorioResult.NotFound();
        }

        usuario.RegistrarApiToken(jti, emitido.ExpiraEm);
        await _usuarios.SaveChangesAsync(cancellationToken);
        return EmitirTokenRelatorioResult.Ok(emitido.Token, emitido.ExpiraEm);
    }
}
