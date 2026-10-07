using AutonomousAudit.Application.Data;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AutonomousAudit.Application.RequestHandlers;

public record BuscarDocumentosDropboxQuery(string? Consulta, string? Caminho)
    : IQuery<BuscarDocumentosDropboxResult>;

public abstract record BuscarDocumentosDropboxResult
{
    public static BuscarDocumentosDropboxResult Ok(IReadOnlyList<DropboxEntrada> itens) =>
        new BuscarDocumentosDropboxOk(itens);

    public static BuscarDocumentosDropboxResult Unauthorized(string message) =>
        new BuscarDocumentosDropboxUnauthorized(message);

    public static BuscarDocumentosDropboxResult Forbidden(string message) =>
        new BuscarDocumentosDropboxForbidden(message);

    public static BuscarDocumentosDropboxResult NotFound(string message) =>
        new BuscarDocumentosDropboxNotFound(message);

    public static BuscarDocumentosDropboxResult Fail(string message) =>
        new BuscarDocumentosDropboxFail(message);
}

public record BuscarDocumentosDropboxOk(IReadOnlyList<DropboxEntrada> Itens) : BuscarDocumentosDropboxResult;
public record BuscarDocumentosDropboxUnauthorized(string Message) : BuscarDocumentosDropboxResult;
public record BuscarDocumentosDropboxForbidden(string Message) : BuscarDocumentosDropboxResult;
public record BuscarDocumentosDropboxNotFound(string Message) : BuscarDocumentosDropboxResult;
public record BuscarDocumentosDropboxFail(string Message) : BuscarDocumentosDropboxResult;

public sealed class BuscarDocumentosDropboxQueryValidator : AbstractValidator<BuscarDocumentosDropboxQuery>
{
    public BuscarDocumentosDropboxQueryValidator()
    {
        RuleFor(x => x.Consulta)
            .MaximumLength(500).WithMessage("A consulta deve ter no maximo 500 caracteres.");

        RuleFor(x => x.Caminho)
            .MaximumLength(1000).WithMessage("O caminho deve ter no maximo 1000 caracteres.");
    }
}

public sealed class BuscarDocumentosDropboxHandler
    : IRequestHandler<BuscarDocumentosDropboxQuery, BuscarDocumentosDropboxResult>
{
    private readonly IDropboxDocumentos _dropbox;
    private readonly ILogger<BuscarDocumentosDropboxHandler> _logger;

    public BuscarDocumentosDropboxHandler(IDropboxDocumentos dropbox, ILogger<BuscarDocumentosDropboxHandler> logger)
    {
        _dropbox = dropbox;
        _logger = logger;
    }

    public async Task<BuscarDocumentosDropboxResult> Handle(
        BuscarDocumentosDropboxQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            var itens = await _dropbox.ListarOuBuscarAsync(request.Consulta, request.Caminho, cancellationToken);
            _logger.LogInformation(
                "Dropbox busca concluida consulta={Consulta} caminho={Caminho} itens={Quantidade}",
                request.Consulta,
                request.Caminho,
                itens.Count);
            return BuscarDocumentosDropboxResult.Ok(itens);
        }
        catch (DropboxStorageException ex) when (ex.Kind == DropboxErrorKind.Unauthorized)
        {
            _logger.LogWarning(ex, "Dropbox nao autorizado na busca");
            return BuscarDocumentosDropboxResult.Unauthorized(ex.Message);
        }
        catch (DropboxStorageException ex) when (ex.Kind == DropboxErrorKind.Forbidden)
        {
            _logger.LogWarning(ex, "Dropbox sem permissao na busca");
            return BuscarDocumentosDropboxResult.Forbidden(ex.Message);
        }
        catch (DropboxStorageException ex) when (ex.Kind == DropboxErrorKind.NotFound)
        {
            _logger.LogWarning("Pasta Dropbox nao encontrada {Caminho}", request.Caminho);
            return BuscarDocumentosDropboxResult.NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha na busca Dropbox consulta={Consulta} caminho={Caminho}", request.Consulta, request.Caminho);
            return BuscarDocumentosDropboxResult.Fail(ex.Message);
        }
    }
}
