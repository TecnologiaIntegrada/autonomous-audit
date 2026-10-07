using AutonomousAudit.Application.Data;
using AutonomousAudit.Domain;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AutonomousAudit.Application.RequestHandlers;

public record EnfileirarPerplexityPromptCommand(
    Guid UsuarioId,
    string? Prompt,
    string? Modelo,
    byte[]? Imagem,
    string? MimeType) : ICommand<EnfileirarPerplexityPromptResult>;

public abstract record EnfileirarPerplexityPromptResult
{
    public static EnfileirarPerplexityPromptResult Accepted(PerplexityPrompt operacao, ArquivoEventoPublicado nats) =>
        new EnfileirarPerplexityPromptAccepted(operacao, nats);

    public static EnfileirarPerplexityPromptResult BadRequest(string message) =>
        new EnfileirarPerplexityPromptBadRequest(message);

    public static EnfileirarPerplexityPromptResult Fail(string message) =>
        new EnfileirarPerplexityPromptFail(message);
}

public record EnfileirarPerplexityPromptAccepted(PerplexityPrompt Operacao, ArquivoEventoPublicado Nats)
    : EnfileirarPerplexityPromptResult;

public record EnfileirarPerplexityPromptBadRequest(string Message) : EnfileirarPerplexityPromptResult;
public record EnfileirarPerplexityPromptFail(string Message) : EnfileirarPerplexityPromptResult;

public sealed class EnfileirarPerplexityPromptCommandValidator : AbstractValidator<EnfileirarPerplexityPromptCommand>
{
    public EnfileirarPerplexityPromptCommandValidator()
    {
        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.Prompt) || x.Imagem is { Length: > 0 })
            .WithMessage("Informe 'prompt' e/ou uma imagem.");

        RuleFor(x => x.Imagem)
            .Must(img => img is null || img.Length <= ConsultarPerplexityAgenteCommandValidator.TamanhoMaximoArquivo)
            .WithMessage("A imagem ou PDF deve ter no maximo 20 MB.");

        RuleFor(x => x.UsuarioId).NotEmpty();
    }
}

public sealed class EnfileirarPerplexityPromptHandler
    : IRequestHandler<EnfileirarPerplexityPromptCommand, EnfileirarPerplexityPromptResult>
{
    private readonly IPerplexityPromptRepository _prompts;
    private readonly IArquivoEventoPublisher _nats;
    private readonly IArquivoStaging _staging;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<EnfileirarPerplexityPromptHandler> _logger;

    public EnfileirarPerplexityPromptHandler(
        IPerplexityPromptRepository prompts,
        IArquivoEventoPublisher nats,
        IArquivoStaging staging,
        IUnitOfWork uow,
        ILogger<EnfileirarPerplexityPromptHandler> logger)
    {
        _prompts = prompts;
        _nats = nats;
        _staging = staging;
        _uow = uow;
        _logger = logger;
    }

    public async Task<EnfileirarPerplexityPromptResult> Handle(
        EnfileirarPerplexityPromptCommand request,
        CancellationToken cancellationToken)
    {
        string? caminho = null;
        try
        {
            var prompt = string.IsNullOrWhiteSpace(request.Prompt)
                ? "Transcreva todo o texto visivel neste arquivo. Mantenha a ordem de leitura."
                : request.Prompt.Trim();
            var mime = ArquivoMime.DetectarMime(request.MimeType, null, request.Imagem);
            var operacao = new PerplexityPrompt(request.UsuarioId, prompt, request.Modelo, mime, null);

            if (request.Imagem is { Length: > 0 })
            {
                await using var buffer = new MemoryStream(request.Imagem, writable: false);
                caminho = await _staging.SalvarAsync(operacao.Id, buffer, cancellationToken);
                operacao.DefinirCaminhoLocal(caminho);
            }

            await _prompts.AddAsync(operacao, cancellationToken);
            await _prompts.SaveChangesAsync(cancellationToken);
            await _uow.CommitAsync(cancellationToken);

            var publicado = await _nats.PublicarPerplexityPromptAsync(
                new PerplexityPromptEvento(operacao.Id),
                cancellationToken);
            _logger.LogInformation(
                "Perplexity prompt enfileirado id={Id} subject={Subject} seq={Seq} imagem={Imagem}",
                operacao.Id,
                publicado.Subject,
                publicado.Sequencia,
                caminho is not null);
            return EnfileirarPerplexityPromptResult.Accepted(operacao, publicado);
        }
        catch (Exception ex)
        {
            if (!string.IsNullOrWhiteSpace(caminho))
            {
                _staging.Excluir(caminho);
            }

            _logger.LogError(ex, "Falha ao enfileirar Perplexity prompt");
            return EnfileirarPerplexityPromptResult.Fail(ex.Message);
        }
    }
}

public record ConsultarPerplexityPromptQuery(Guid Id) : IQuery<ConsultarPerplexityPromptResult>;

public abstract record ConsultarPerplexityPromptResult
{
    public static ConsultarPerplexityPromptResult Ok(PerplexityPrompt operacao) => new ConsultarPerplexityPromptOk(operacao);
    public static ConsultarPerplexityPromptResult NotFound(string message) => new ConsultarPerplexityPromptNotFound(message);
    public static ConsultarPerplexityPromptResult Fail(string message) => new ConsultarPerplexityPromptFail(message);
}

public record ConsultarPerplexityPromptOk(PerplexityPrompt Operacao) : ConsultarPerplexityPromptResult;
public record ConsultarPerplexityPromptNotFound(string Message) : ConsultarPerplexityPromptResult;
public record ConsultarPerplexityPromptFail(string Message) : ConsultarPerplexityPromptResult;

public sealed class ConsultarPerplexityPromptQueryValidator : AbstractValidator<ConsultarPerplexityPromptQuery>
{
    public ConsultarPerplexityPromptQueryValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public sealed class ConsultarPerplexityPromptHandler
    : IRequestHandler<ConsultarPerplexityPromptQuery, ConsultarPerplexityPromptResult>
{
    private readonly IPerplexityPromptRepository _prompts;
    private readonly ILogger<ConsultarPerplexityPromptHandler> _logger;

    public ConsultarPerplexityPromptHandler(
        IPerplexityPromptRepository prompts,
        ILogger<ConsultarPerplexityPromptHandler> logger)
    {
        _prompts = prompts;
        _logger = logger;
    }

    public async Task<ConsultarPerplexityPromptResult> Handle(
        ConsultarPerplexityPromptQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            var operacao = await _prompts.GetByIdAsync(request.Id, cancellationToken);
            if (operacao is null)
            {
                return ConsultarPerplexityPromptResult.NotFound("Operacao Perplexity prompt nao encontrada.");
            }

            return ConsultarPerplexityPromptResult.Ok(operacao);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao consultar perplexity_prompt {Id}", request.Id);
            return ConsultarPerplexityPromptResult.Fail(ex.Message);
        }
    }
}

public record EnfileirarPerplexityArquivoCommand(
    Guid UsuarioId,
    string UsuarioEmail,
    string? Prompt,
    string? Modelo,
    string NomeOriginal,
    string? MimeType,
    long TamanhoBytes,
    Stream Conteudo) : ICommand<EnfileirarPerplexityArquivoResult>;

public abstract record EnfileirarPerplexityArquivoResult
{
    public static EnfileirarPerplexityArquivoResult Accepted(PerplexityArquivo operacao, ArquivoEventoPublicado nats) =>
        new EnfileirarPerplexityArquivoAccepted(operacao, nats);

    public static EnfileirarPerplexityArquivoResult BadRequest(string message) =>
        new EnfileirarPerplexityArquivoBadRequest(message);

    public static EnfileirarPerplexityArquivoResult Fail(string message) =>
        new EnfileirarPerplexityArquivoFail(message);
}

public record EnfileirarPerplexityArquivoAccepted(PerplexityArquivo Operacao, ArquivoEventoPublicado Nats)
    : EnfileirarPerplexityArquivoResult;

public record EnfileirarPerplexityArquivoBadRequest(string Message) : EnfileirarPerplexityArquivoResult;
public record EnfileirarPerplexityArquivoFail(string Message) : EnfileirarPerplexityArquivoResult;

public sealed class EnfileirarPerplexityArquivoCommandValidator : AbstractValidator<EnfileirarPerplexityArquivoCommand>
{
    public EnfileirarPerplexityArquivoCommandValidator()
    {
        RuleFor(x => x.UsuarioId).NotEmpty();
        RuleFor(x => x.NomeOriginal).NotEmpty().WithMessage("O arquivo e obrigatorio.");
        RuleFor(x => x.TamanhoBytes).GreaterThan(0).WithMessage("O arquivo nao pode estar vazio.");
        RuleFor(x => x.Conteudo).NotNull();
    }
}

public sealed class EnfileirarPerplexityArquivoHandler
    : IRequestHandler<EnfileirarPerplexityArquivoCommand, EnfileirarPerplexityArquivoResult>
{
    private readonly IArquivoRecebidoRepository _arquivos;
    private readonly IDropboxDispatchRepository _dispatches;
    private readonly IPerplexityArquivoRepository _perplexity;
    private readonly IArquivoEventoPublisher _nats;
    private readonly IArquivoStaging _staging;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<EnfileirarPerplexityArquivoHandler> _logger;

    public EnfileirarPerplexityArquivoHandler(
        IArquivoRecebidoRepository arquivos,
        IDropboxDispatchRepository dispatches,
        IPerplexityArquivoRepository perplexity,
        IArquivoEventoPublisher nats,
        IArquivoStaging staging,
        IUnitOfWork uow,
        ILogger<EnfileirarPerplexityArquivoHandler> logger)
    {
        _arquivos = arquivos;
        _dispatches = dispatches;
        _perplexity = perplexity;
        _nats = nats;
        _staging = staging;
        _uow = uow;
        _logger = logger;
    }

    public async Task<EnfileirarPerplexityArquivoResult> Handle(
        EnfileirarPerplexityArquivoCommand request,
        CancellationToken cancellationToken)
    {
        string? caminho = null;
        try
        {
            var prompt = string.IsNullOrWhiteSpace(request.Prompt)
                ? "Transcreva todo o texto visivel neste arquivo. Mantenha a ordem de leitura."
                : request.Prompt.Trim();
            var nomeOriginal = DropboxPathHelper.NomeOriginal(request.NomeOriginal);
            var tamanhoMb = Math.Round(request.TamanhoBytes / (1024m * 1024m), 6, MidpointRounding.AwayFromZero);
            var arquivo = new ArquivoRecebido(
                nomeOriginal,
                tamanhoMb,
                DateTimeOffset.UtcNow,
                request.UsuarioEmail);
            var nomeDropbox = DropboxPathHelper.NomeDropboxPorId(arquivo.Id, nomeOriginal);
            var destino = DropboxPathHelper.DestinoNaRaiz(nomeDropbox);
            caminho = await _staging.SalvarAsync(arquivo.Id, request.Conteudo, cancellationToken);
            if (new FileInfo(caminho).Length == 0)
            {
                _staging.Excluir(caminho);
                return EnfileirarPerplexityArquivoResult.BadRequest("O arquivo nao pode estar vazio.");
            }

            var dispatch = new DropboxDispatch(
                arquivo.Id,
                arquivo.Id,
                nomeDropbox,
                nomeOriginal,
                caminho,
                destino);
            var mime = ArquivoMime.DetectarMime(request.MimeType, nomeOriginal, null);
            var operacao = new PerplexityArquivo(
                request.UsuarioId,
                arquivo.Id,
                dispatch.Id,
                prompt,
                request.Modelo,
                mime,
                nomeOriginal,
                nomeDropbox,
                caminho,
                destino);

            await _arquivos.AddAsync(arquivo, cancellationToken);
            await _dispatches.AddAsync(dispatch, cancellationToken);
            await _perplexity.AddAsync(operacao, cancellationToken);
            await _arquivos.SaveChangesAsync(cancellationToken);
            await _uow.CommitAsync(cancellationToken);

            var publicado = await _nats.PublicarPerplexityArquivoAsync(
                new PerplexityArquivoEvento(operacao.Id),
                cancellationToken);
            _logger.LogInformation(
                "Perplexity arquivo enfileirado id={Id} arquivoId={ArquivoId} dropbox={DropboxId} destino={Destino} seq={Seq}",
                operacao.Id,
                arquivo.Id,
                dispatch.Id,
                destino,
                publicado.Sequencia);
            return EnfileirarPerplexityArquivoResult.Accepted(operacao, publicado);
        }
        catch (Exception ex)
        {
            if (!string.IsNullOrWhiteSpace(caminho))
            {
                _staging.Excluir(caminho);
            }

            _logger.LogError(ex, "Falha ao enfileirar Perplexity arquivo");
            return EnfileirarPerplexityArquivoResult.Fail(ex.Message);
        }
    }
}

public record ConsultarPerplexityArquivoQuery(Guid Id) : IQuery<ConsultarPerplexityArquivoResult>;

public abstract record ConsultarPerplexityArquivoResult
{
    public static ConsultarPerplexityArquivoResult Ok(PerplexityArquivo operacao) =>
        new ConsultarPerplexityArquivoOk(operacao);

    public static ConsultarPerplexityArquivoResult NotFound(string message) =>
        new ConsultarPerplexityArquivoNotFound(message);

    public static ConsultarPerplexityArquivoResult Fail(string message) =>
        new ConsultarPerplexityArquivoFail(message);
}

public record ConsultarPerplexityArquivoOk(PerplexityArquivo Operacao) : ConsultarPerplexityArquivoResult;
public record ConsultarPerplexityArquivoNotFound(string Message) : ConsultarPerplexityArquivoResult;
public record ConsultarPerplexityArquivoFail(string Message) : ConsultarPerplexityArquivoResult;

public sealed class ConsultarPerplexityArquivoQueryValidator : AbstractValidator<ConsultarPerplexityArquivoQuery>
{
    public ConsultarPerplexityArquivoQueryValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public sealed class ConsultarPerplexityArquivoHandler
    : IRequestHandler<ConsultarPerplexityArquivoQuery, ConsultarPerplexityArquivoResult>
{
    private readonly IPerplexityArquivoRepository _arquivos;
    private readonly ILogger<ConsultarPerplexityArquivoHandler> _logger;

    public ConsultarPerplexityArquivoHandler(
        IPerplexityArquivoRepository arquivos,
        ILogger<ConsultarPerplexityArquivoHandler> logger)
    {
        _arquivos = arquivos;
        _logger = logger;
    }

    public async Task<ConsultarPerplexityArquivoResult> Handle(
        ConsultarPerplexityArquivoQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            var operacao = await _arquivos.GetByIdAsync(request.Id, cancellationToken);
            if (operacao is null)
            {
                return ConsultarPerplexityArquivoResult.NotFound("Operacao Perplexity arquivo nao encontrada.");
            }

            return ConsultarPerplexityArquivoResult.Ok(operacao);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao consultar perplexity_arquivo {Id}", request.Id);
            return ConsultarPerplexityArquivoResult.Fail(ex.Message);
        }
    }
}
