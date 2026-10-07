using AutonomousAudit.Application.Data;
using AutonomousAudit.Domain;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AutonomousAudit.Application.RequestHandlers;

public record ConsultarDropboxDispatchQuery(Guid TransacaoId) : IQuery<ConsultarDropboxDispatchResult>;

public abstract record ConsultarDropboxDispatchResult
{
    public static ConsultarDropboxDispatchResult Ok(DropboxDispatch dispatch) =>
        new ConsultarDropboxDispatchOk(dispatch);

    public static ConsultarDropboxDispatchResult NotFound(string message) =>
        new ConsultarDropboxDispatchNotFound(message);

    public static ConsultarDropboxDispatchResult Fail(string message) =>
        new ConsultarDropboxDispatchFail(message);
}

public record ConsultarDropboxDispatchOk(DropboxDispatch Dispatch) : ConsultarDropboxDispatchResult;
public record ConsultarDropboxDispatchNotFound(string Message) : ConsultarDropboxDispatchResult;
public record ConsultarDropboxDispatchFail(string Message) : ConsultarDropboxDispatchResult;

public sealed class ConsultarDropboxDispatchQueryValidator : AbstractValidator<ConsultarDropboxDispatchQuery>
{
    public ConsultarDropboxDispatchQueryValidator()
    {
        RuleFor(x => x.TransacaoId).NotEmpty();
    }
}

public sealed class ConsultarDropboxDispatchHandler
    : IRequestHandler<ConsultarDropboxDispatchQuery, ConsultarDropboxDispatchResult>
{
    private readonly IDropboxDispatchConsulta _dispatches;
    private readonly ILogger<ConsultarDropboxDispatchHandler> _logger;

    public ConsultarDropboxDispatchHandler(
        IDropboxDispatchConsulta dispatches,
        ILogger<ConsultarDropboxDispatchHandler> logger)
    {
        _dispatches = dispatches;
        _logger = logger;
    }

    public async Task<ConsultarDropboxDispatchResult> Handle(
        ConsultarDropboxDispatchQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            var dispatch = await _dispatches.ObterPorIdAsync(request.TransacaoId, cancellationToken);
            if (dispatch is null)
            {
                _logger.LogWarning("Transacao Dropbox nao encontrada {TransacaoId}", request.TransacaoId);
                return ConsultarDropboxDispatchResult.NotFound("Transacao de envio ao armazenamento nao encontrada.");
            }

            _logger.LogInformation(
                "Status Dropbox transacao={TransacaoId} status={Status} tentativas={Tentativas}/{Max} erro={Erro}",
                dispatch.Id,
                dispatch.Status,
                dispatch.Tentativas,
                dispatch.MaxTentativas,
                dispatch.UltimaMensagemErro);

            return ConsultarDropboxDispatchResult.Ok(dispatch);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao consultar dropbox_dispatch {TransacaoId}", request.TransacaoId);
            return ConsultarDropboxDispatchResult.Fail(ex.Message);
        }
    }
}
