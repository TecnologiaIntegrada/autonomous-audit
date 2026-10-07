using AutonomousAudit.Application.Data;
using AutonomousAudit.Domain;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AutonomousAudit.Application.RequestHandlers;

public record ListarContasEmailQuery : IQuery<ListarContasEmailResult>;

public abstract record ListarContasEmailResult
{
    public static ListarContasEmailResult Ok(IReadOnlyList<ContaEmail> contas) => new ListarContasEmailOk(contas);
    public static ListarContasEmailResult Fail(string message) => new ListarContasEmailFail(message);
}

public record ListarContasEmailOk(IReadOnlyList<ContaEmail> Contas) : ListarContasEmailResult;
public record ListarContasEmailFail(string Message) : ListarContasEmailResult;

public sealed class ListarContasEmailHandler : IRequestHandler<ListarContasEmailQuery, ListarContasEmailResult>
{
    private readonly IContaEmailConsulta _consulta;
    private readonly ILogger<ListarContasEmailHandler> _logger;

    public ListarContasEmailHandler(IContaEmailConsulta consulta, ILogger<ListarContasEmailHandler> logger)
    {
        _consulta = consulta;
        _logger = logger;
    }

    public async Task<ListarContasEmailResult> Handle(ListarContasEmailQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var contas = await _consulta.ListarAsync(cancellationToken);
            _logger.LogInformation("Contas SMTP listadas quantidade={Quantidade}", contas.Count);
            return ListarContasEmailResult.Ok(contas);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao listar contas SMTP");
            return ListarContasEmailResult.Fail(ex.Message);
        }
    }
}
