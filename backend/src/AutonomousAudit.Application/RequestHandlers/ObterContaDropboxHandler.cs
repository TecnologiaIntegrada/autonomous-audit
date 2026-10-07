using AutonomousAudit.Application.Data;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AutonomousAudit.Application.RequestHandlers;

public record ObterContaDropboxQuery : IQuery<ObterContaDropboxResult>;

public abstract record ObterContaDropboxResult
{
    public static ObterContaDropboxResult Ok(DropboxContaAtual conta) => new ObterContaDropboxOk(conta);
    public static ObterContaDropboxResult Unauthorized(string message) => new ObterContaDropboxUnauthorized(message);
    public static ObterContaDropboxResult Forbidden(string message) => new ObterContaDropboxForbidden(message);
    public static ObterContaDropboxResult Fail(string message) => new ObterContaDropboxFail(message);
}

public record ObterContaDropboxOk(DropboxContaAtual Conta) : ObterContaDropboxResult;
public record ObterContaDropboxUnauthorized(string Message) : ObterContaDropboxResult;
public record ObterContaDropboxForbidden(string Message) : ObterContaDropboxResult;
public record ObterContaDropboxFail(string Message) : ObterContaDropboxResult;

public sealed class ObterContaDropboxHandler : IRequestHandler<ObterContaDropboxQuery, ObterContaDropboxResult>
{
    private readonly IDropboxDocumentos _dropbox;
    private readonly ILogger<ObterContaDropboxHandler> _logger;

    public ObterContaDropboxHandler(IDropboxDocumentos dropbox, ILogger<ObterContaDropboxHandler> logger)
    {
        _dropbox = dropbox;
        _logger = logger;
    }

    public async Task<ObterContaDropboxResult> Handle(ObterContaDropboxQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var conta = await _dropbox.ObterContaAsync(cancellationToken);
            _logger.LogInformation("Conta Dropbox vinculada {Email} {TipoConta}", conta.Email, conta.TipoConta);
            return ObterContaDropboxResult.Ok(conta);
        }
        catch (DropboxStorageException ex) when (ex.Kind == DropboxErrorKind.Unauthorized)
        {
            _logger.LogWarning(ex, "Token Dropbox nao autorizado ao consultar a conta");
            return ObterContaDropboxResult.Unauthorized(ex.Message);
        }
        catch (DropboxStorageException ex) when (ex.Kind == DropboxErrorKind.Forbidden)
        {
            _logger.LogWarning(ex, "Permissao insuficiente no Dropbox ao consultar a conta");
            return ObterContaDropboxResult.Forbidden(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao consultar a conta Dropbox");
            return ObterContaDropboxResult.Fail(ex.Message);
        }
    }
}
