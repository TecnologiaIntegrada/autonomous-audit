using AutonomousAudit.Application;
using AutonomousAudit.Application.Data;
using AutonomousAudit.Domain;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AutonomousAudit.Application.RequestHandlers;

public record EnviarSmsCommand(string Numero, string Mensagem, string? Referencia)
    : ICommand<EnviarSmsResult>;

public abstract record EnviarSmsResult
{
    public static EnviarSmsResult Ok(SmsEnvioResultado envio) => new EnviarSmsOk(envio);
    public static EnviarSmsResult BadRequest(string message) => new EnviarSmsBadRequest(message);
    public static EnviarSmsResult Unauthorized(string message) => new EnviarSmsUnauthorized(message);
    public static EnviarSmsResult Fail(string message) => new EnviarSmsFail(message);
}

public record EnviarSmsOk(SmsEnvioResultado Envio) : EnviarSmsResult;
public record EnviarSmsBadRequest(string Message) : EnviarSmsResult;
public record EnviarSmsUnauthorized(string Message) : EnviarSmsResult;
public record EnviarSmsFail(string Message) : EnviarSmsResult;

public sealed class EnviarSmsCommandValidator : AbstractValidator<EnviarSmsCommand>
{
    public EnviarSmsCommandValidator()
    {
        RuleFor(x => x.Numero)
            .NotEmpty().WithMessage("Informe o numero do destinatario.")
            .Must(n => SmsNumero.QuantidadeDigitos(n) is >= 10 and <= 13)
            .WithMessage("Informe um telefone com DDD, com 10 a 13 digitos (ex.: 11988887777 ou 5511988887777).");

        RuleFor(x => x.Mensagem)
            .NotEmpty().WithMessage("Informe o texto da mensagem.")
            .MaximumLength(1600).WithMessage("A mensagem deve ter no maximo 1600 caracteres.");

        RuleFor(x => x.Referencia)
            .MaximumLength(100)
            .When(x => !string.IsNullOrWhiteSpace(x.Referencia));
    }
}

public sealed class EnviarSmsHandler : IRequestHandler<EnviarSmsCommand, EnviarSmsResult>
{
    private readonly ISmsDevClient _sms;
    private readonly ILogger<EnviarSmsHandler> _logger;

    public EnviarSmsHandler(ISmsDevClient sms, ILogger<EnviarSmsHandler> logger)
    {
        _sms = sms;
        _logger = logger;
    }

    public async Task<EnviarSmsResult> Handle(EnviarSmsCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var numero = SmsNumero.Normalizar(request.Numero);
            _logger.LogInformation(
                "Enviando SMS para {NumeroSufixo} caracteres={Tamanho}",
                SmsNumero.Sufixo(numero),
                request.Mensagem.Length);

            var envio = await _sms.EnviarAsync(numero, request.Mensagem.Trim(), request.Referencia, cancellationToken);
            if (!envio.Situacao.Equals("OK", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "SMSDev recusou o envio id={Id} codigoSmsDev={Codigo} situacao={Situacao} numero={NumeroSufixo} descricao={Descricao}",
                    envio.Id,
                    envio.Codigo,
                    envio.Situacao,
                    SmsNumero.Sufixo(numero),
                    envio.Descricao);
                return EnviarSmsResult.Fail(
                    string.IsNullOrWhiteSpace(envio.Descricao)
                        ? $"SMSDev recusou o envio (situacao={envio.Situacao}, codigo={envio.Codigo})."
                        : $"SMSDev recusou o envio (codigo={envio.Codigo}): {envio.Descricao}");
            }

            _logger.LogInformation(
                "SMS aceito pela SMSDev id={Id} codigoSmsDev={Codigo} descricao={Descricao} numero={NumeroSufixo}",
                envio.Id,
                envio.Codigo,
                envio.Descricao,
                SmsNumero.Sufixo(numero));
            return EnviarSmsResult.Ok(envio);
        }
        catch (SmsDevException ex) when (ex.Kind == SmsDevErrorKind.Unauthorized)
        {
            _logger.LogWarning(
                ex,
                "Token SMSDev nao autorizado httpStatus={HttpStatus} body={Body}",
                ex.HttpStatus,
                ex.ResponseBody);
            return EnviarSmsResult.Unauthorized(ex.Message);
        }
        catch (SmsDevException ex) when (ex.Kind == SmsDevErrorKind.InvalidRequest)
        {
            _logger.LogWarning(
                ex,
                "Envio SMS invalido httpStatus={HttpStatus} body={Body} cadeia={Cadeia}",
                ex.HttpStatus,
                ex.ResponseBody,
                LogExcecao.Cadeia(ex));
            return EnviarSmsResult.BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            var smsEx = ex as SmsDevException;
            _logger.LogError(
                ex,
                "Falha ao enviar SMS numero={NumeroSufixo} httpStatus={HttpStatus} body={Body} cadeia={Cadeia}",
                SmsNumero.Sufixo(SmsNumero.Normalizar(request.Numero)),
                smsEx?.HttpStatus,
                smsEx?.ResponseBody,
                LogExcecao.Cadeia(ex));
            return EnviarSmsResult.Fail(ex.Message);
        }
    }
}

public static class SmsNumero
{
    public static string Normalizar(string? numero) =>
        TelefoneContato.Normalizar(numero) ?? string.Empty;

    public static int QuantidadeDigitos(string? numero) =>
        TelefoneContato.QuantidadeDigitos(numero);

    public static string Sufixo(string numero)
    {
        var digitos = new string(numero.Where(char.IsDigit).ToArray());
        return digitos.Length <= 4 ? "****" : "****" + digitos[^4..];
    }
}
