using AutonomousAudit.Application;
using AutonomousAudit.Application.Data;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AutonomousAudit.Application.RequestHandlers;

public record ConsultarSmsQuery(string Id) : IQuery<ConsultarSmsResult>;

public abstract record ConsultarSmsResult
{
    public static ConsultarSmsResult Ok(SmsStatusResultado status) => new ConsultarSmsOk(status);
    public static ConsultarSmsResult BadRequest(string message) => new ConsultarSmsBadRequest(message);
    public static ConsultarSmsResult Unauthorized(string message) => new ConsultarSmsUnauthorized(message);
    public static ConsultarSmsResult NotFound(string message) => new ConsultarSmsNotFound(message);
    public static ConsultarSmsResult Fail(string message) => new ConsultarSmsFail(message);
}

public record ConsultarSmsOk(SmsStatusResultado Status) : ConsultarSmsResult;
public record ConsultarSmsBadRequest(string Message) : ConsultarSmsResult;
public record ConsultarSmsUnauthorized(string Message) : ConsultarSmsResult;
public record ConsultarSmsNotFound(string Message) : ConsultarSmsResult;
public record ConsultarSmsFail(string Message) : ConsultarSmsResult;

public sealed class ConsultarSmsQueryValidator : AbstractValidator<ConsultarSmsQuery>
{
    public ConsultarSmsQueryValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Informe o id da mensagem SMSDev.");
    }
}

public sealed class ConsultarSmsHandler : IRequestHandler<ConsultarSmsQuery, ConsultarSmsResult>
{
    private readonly ISmsDevClient _sms;
    private readonly ILogger<ConsultarSmsHandler> _logger;

    public ConsultarSmsHandler(ISmsDevClient sms, ILogger<ConsultarSmsHandler> logger)
    {
        _sms = sms;
        _logger = logger;
    }

    public async Task<ConsultarSmsResult> Handle(ConsultarSmsQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var status = await _sms.ConsultarAsync(request.Id.Trim(), cancellationToken);
            _logger.LogInformation(
                "Status SMS id={Id} descricao={Descricao} operadora={Operadora}",
                status.Id,
                status.Descricao,
                status.Operadora);
            return ConsultarSmsResult.Ok(status);
        }
        catch (SmsDevException ex) when (ex.Kind == SmsDevErrorKind.Unauthorized)
        {
            _logger.LogWarning(
                ex,
                "Token SMSDev nao autorizado na consulta id={Id} httpStatus={HttpStatus} body={Body}",
                request.Id,
                ex.HttpStatus,
                ex.ResponseBody);
            return ConsultarSmsResult.Unauthorized(ex.Message);
        }
        catch (SmsDevException ex) when (ex.Kind == SmsDevErrorKind.NotFound)
        {
            _logger.LogWarning(
                ex,
                "SMS nao encontrado id={Id} httpStatus={HttpStatus} body={Body}",
                request.Id,
                ex.HttpStatus,
                ex.ResponseBody);
            return ConsultarSmsResult.NotFound(ex.Message);
        }
        catch (SmsDevException ex) when (ex.Kind == SmsDevErrorKind.InvalidRequest)
        {
            _logger.LogWarning(
                ex,
                "Consulta SMS invalida id={Id} httpStatus={HttpStatus} body={Body}",
                request.Id,
                ex.HttpStatus,
                ex.ResponseBody);
            return ConsultarSmsResult.BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            var smsEx = ex as SmsDevException;
            _logger.LogError(
                ex,
                "Falha ao consultar SMS id={Id} httpStatus={HttpStatus} body={Body} cadeia={Cadeia}",
                request.Id,
                smsEx?.HttpStatus,
                smsEx?.ResponseBody,
                LogExcecao.Cadeia(ex));
            return ConsultarSmsResult.Fail(ex.Message);
        }
    }
}
