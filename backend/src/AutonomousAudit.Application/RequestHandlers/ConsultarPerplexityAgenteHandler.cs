using AutonomousAudit.Application.Data;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AutonomousAudit.Application.RequestHandlers;

public record ConsultarPerplexityAgenteCommand(
    string? Prompt,
    string? Modelo,
    byte[]? Arquivo,
    string? MimeType,
    Guid? UsuarioId = null) : IQuery<ConsultarPerplexityAgenteResult>;

public abstract record ConsultarPerplexityAgenteResult
{
    public static ConsultarPerplexityAgenteResult Ok(PerplexityAgenteResposta resposta) =>
        new ConsultarPerplexityAgenteOk(resposta);

    public static ConsultarPerplexityAgenteResult BadRequest(string message) =>
        new ConsultarPerplexityAgenteBadRequest(message);

    public static ConsultarPerplexityAgenteResult Unauthorized(string message) =>
        new ConsultarPerplexityAgenteUnauthorized(message);

    public static ConsultarPerplexityAgenteResult Fail(string message) =>
        new ConsultarPerplexityAgenteFail(message);
}

public record ConsultarPerplexityAgenteOk(PerplexityAgenteResposta Resposta) : ConsultarPerplexityAgenteResult;
public record ConsultarPerplexityAgenteBadRequest(string Message) : ConsultarPerplexityAgenteResult;
public record ConsultarPerplexityAgenteUnauthorized(string Message) : ConsultarPerplexityAgenteResult;
public record ConsultarPerplexityAgenteFail(string Message) : ConsultarPerplexityAgenteResult;

public sealed class ConsultarPerplexityAgenteCommandValidator : AbstractValidator<ConsultarPerplexityAgenteCommand>
{
    public const int TamanhoMaximoArquivo = 20 * 1024 * 1024;

    public ConsultarPerplexityAgenteCommandValidator()
    {
        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.Prompt) || x.Arquivo is { Length: > 0 })
            .WithMessage("Informe 'prompt' e/ou uma imagem ou PDF.");

        RuleFor(x => x.Arquivo)
            .Must(arq => arq is null || arq.Length <= TamanhoMaximoArquivo)
            .WithMessage("A imagem ou PDF deve ter no maximo 20 MB.");
    }
}

public sealed class ConsultarPerplexityAgenteHandler
    : IRequestHandler<ConsultarPerplexityAgenteCommand, ConsultarPerplexityAgenteResult>
{
    private readonly IPerplexityClient _client;
    private readonly IRegistrarPerplexityUsoUsuario _uso;
    private readonly ILogger<ConsultarPerplexityAgenteHandler> _logger;

    public ConsultarPerplexityAgenteHandler(
        IPerplexityClient client,
        IRegistrarPerplexityUsoUsuario uso,
        ILogger<ConsultarPerplexityAgenteHandler> logger)
    {
        _client = client;
        _uso = uso;
        _logger = logger;
    }

    public async Task<ConsultarPerplexityAgenteResult> Handle(
        ConsultarPerplexityAgenteCommand request,
        CancellationToken cancellationToken)
    {
        var prompt = string.IsNullOrWhiteSpace(request.Prompt)
            ? "Transcreva todo o texto visivel neste arquivo. Mantenha a ordem de leitura."
            : request.Prompt.Trim();

        try
        {
            _logger.LogInformation(
                "Consulta Perplexity Agent iniciada modelo={Modelo} promptChars={Prompt} arquivo={Arquivo} mime={Mime} bytes={Bytes}",
                PerplexityModelos.NormalizarAgente(request.Modelo),
                prompt.Length,
                request.Arquivo is { Length: > 0 },
                request.MimeType,
                request.Arquivo?.Length ?? 0);

            var resposta = await _client.AnalisarAsync(
                new PerplexityAgentePedido(prompt, request.Modelo, request.Arquivo, request.MimeType),
                cancellationToken);

            if (request.UsuarioId is Guid usuarioId)
            {
                await _uso.RegistrarAsync(usuarioId, resposta, cancellationToken);
            }

            return ConsultarPerplexityAgenteResult.Ok(resposta);
        }
        catch (PerplexityException ex) when (ex.Kind == PerplexityErrorKind.Unauthorized)
        {
            return ConsultarPerplexityAgenteResult.Unauthorized(ex.Message);
        }
        catch (PerplexityException ex) when (ex.Kind == PerplexityErrorKind.InvalidRequest)
        {
            return ConsultarPerplexityAgenteResult.BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha na consulta Perplexity Agent");
            return ConsultarPerplexityAgenteResult.Fail(ex.Message);
        }
    }
}

public static class PerplexityModelos
{
    public const string AgentePadrao = "openai/gpt-5-mini";

    public static string NormalizarAgente(string? modelo)
    {
        var valor = (modelo ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(valor)
            || valor.StartsWith("gemini", StringComparison.OrdinalIgnoreCase)
            || valor.StartsWith("qwen", StringComparison.OrdinalIgnoreCase)
            || valor.Equals("sonar", StringComparison.OrdinalIgnoreCase))
        {
            return AgentePadrao;
        }

        return valor;
    }
}
