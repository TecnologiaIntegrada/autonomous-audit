using AutonomousAudit.Application.Data;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AutonomousAudit.Application.RequestHandlers;

public record ExcluirDocumentoDropboxCommand(string Caminho) : ICommand<ExcluirDocumentoDropboxResult>;

public abstract record ExcluirDocumentoDropboxResult
{
    public static ExcluirDocumentoDropboxResult Ok(DropboxEntrada arquivo) =>
        new ExcluirDocumentoDropboxOk(arquivo);

    public static ExcluirDocumentoDropboxResult BadRequest(string message) =>
        new ExcluirDocumentoDropboxBadRequest(message);

    public static ExcluirDocumentoDropboxResult Unauthorized(string message) =>
        new ExcluirDocumentoDropboxUnauthorized(message);

    public static ExcluirDocumentoDropboxResult Forbidden(string message) =>
        new ExcluirDocumentoDropboxForbidden(message);

    public static ExcluirDocumentoDropboxResult NotFound(string message) =>
        new ExcluirDocumentoDropboxNotFound(message);

    public static ExcluirDocumentoDropboxResult Fail(string message) =>
        new ExcluirDocumentoDropboxFail(message);
}

public record ExcluirDocumentoDropboxOk(DropboxEntrada Arquivo) : ExcluirDocumentoDropboxResult;
public record ExcluirDocumentoDropboxBadRequest(string Message) : ExcluirDocumentoDropboxResult;
public record ExcluirDocumentoDropboxUnauthorized(string Message) : ExcluirDocumentoDropboxResult;
public record ExcluirDocumentoDropboxForbidden(string Message) : ExcluirDocumentoDropboxResult;
public record ExcluirDocumentoDropboxNotFound(string Message) : ExcluirDocumentoDropboxResult;
public record ExcluirDocumentoDropboxFail(string Message) : ExcluirDocumentoDropboxResult;

public sealed class ExcluirDocumentoDropboxCommandValidator : AbstractValidator<ExcluirDocumentoDropboxCommand>
{
    public ExcluirDocumentoDropboxCommandValidator()
    {
        RuleFor(x => x.Caminho)
            .NotEmpty().WithMessage("Informe o caminho do arquivo no armazenamento.")
            .MaximumLength(1000).WithMessage("O caminho deve ter no maximo 1000 caracteres.");
    }
}

public sealed class ExcluirDocumentoDropboxHandler
    : IRequestHandler<ExcluirDocumentoDropboxCommand, ExcluirDocumentoDropboxResult>
{
    private readonly IDropboxDocumentos _dropbox;
    private readonly ILogger<ExcluirDocumentoDropboxHandler> _logger;

    public ExcluirDocumentoDropboxHandler(IDropboxDocumentos dropbox, ILogger<ExcluirDocumentoDropboxHandler> logger)
    {
        _dropbox = dropbox;
        _logger = logger;
    }

    public async Task<ExcluirDocumentoDropboxResult> Handle(
        ExcluirDocumentoDropboxCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            var caminho = DropboxPathHelper.Normalizar(request.Caminho);
            if (string.IsNullOrEmpty(caminho))
            {
                _logger.LogWarning("Exclusao Dropbox rejeitada: tentativa de apagar a raiz");
                return ExcluirDocumentoDropboxResult.BadRequest("Nao e permitido excluir a raiz do armazenamento.");
            }

            var excluido = await _dropbox.ExcluirAsync(caminho, cancellationToken);
            _logger.LogInformation("Arquivo excluido no Dropbox {Caminho}", excluido.Caminho);
            return ExcluirDocumentoDropboxResult.Ok(excluido);
        }
        catch (DropboxStorageException ex) when (ex.Kind == DropboxErrorKind.Unauthorized)
        {
            _logger.LogWarning(ex, "Dropbox nao autorizado na exclusao {Caminho}", request.Caminho);
            return ExcluirDocumentoDropboxResult.Unauthorized(ex.Message);
        }
        catch (DropboxStorageException ex) when (ex.Kind == DropboxErrorKind.Forbidden)
        {
            _logger.LogWarning(ex, "Dropbox sem permissao na exclusao {Caminho}", request.Caminho);
            return ExcluirDocumentoDropboxResult.Forbidden(ex.Message);
        }
        catch (DropboxStorageException ex) when (ex.Kind == DropboxErrorKind.NotFound)
        {
            _logger.LogWarning("Arquivo Dropbox nao encontrado para exclusao {Caminho}", request.Caminho);
            return ExcluirDocumentoDropboxResult.NotFound(ex.Message);
        }
        catch (DropboxStorageException ex) when (ex.Kind == DropboxErrorKind.InvalidRequest)
        {
            _logger.LogWarning(ex, "Exclusao Dropbox invalida {Caminho}", request.Caminho);
            return ExcluirDocumentoDropboxResult.BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao excluir arquivo no Dropbox {Caminho}", request.Caminho);
            return ExcluirDocumentoDropboxResult.Fail(ex.Message);
        }
    }
}
