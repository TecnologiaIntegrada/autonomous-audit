using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AutonomousAudit.Application;

public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;
    private readonly ILogger<ValidationBehavior<TRequest, TResponse>> _logger;

    public ValidationBehavior(
        IEnumerable<IValidator<TRequest>> validators,
        ILogger<ValidationBehavior<TRequest, TResponse>> logger)
    {
        _validators = validators;
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (_validators.Any())
        {
            var context = new ValidationContext<TRequest>(request);
            var failures = _validators
                .Select(v => v.Validate(context))
                .SelectMany(r => r.Errors)
                .Where(f => f is not null)
                .ToList();

            if (failures.Count > 0)
            {
                _logger.LogWarning(
                    "Validacao falhou request={RequestType} erros={Erros} valores={Valores}",
                    typeof(TRequest).Name,
                    string.Join("; ", failures.Select(f => $"{f.PropertyName}: {f.ErrorMessage}")),
                    string.Join("; ", failures.Select(f => $"{f.PropertyName}={ValorParaLog(f.AttemptedValue)}")));
                throw new ValidationException(failures);
            }
        }

        return await next();
    }

    private static string ValorParaLog(object? valor)
    {
        if (valor is null)
        {
            return "(null)";
        }

        var texto = Convert.ToString(valor) ?? string.Empty;
        if (texto.Length > 120)
        {
            texto = texto[..120] + "...";
        }

        return texto.Replace('\r', ' ').Replace('\n', ' ');
    }
}
