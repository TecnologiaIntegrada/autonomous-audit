using AutonomousAudit.Application.Data;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AutonomousAudit.Application.RequestHandlers;

public record ConsultarPerplexityCommand(
    string? Pergunta,
    string? Contexto,
    IReadOnlyList<PerplexityMensagem>? Mensagens) : IQuery<ConsultarPerplexityResult>;

public abstract record ConsultarPerplexityResult
{
    public static ConsultarPerplexityResult Ok(PerplexityResposta resposta) => new ConsultarPerplexityOk(resposta);
    public static ConsultarPerplexityResult BadRequest(string message) => new ConsultarPerplexityBadRequest(message);
    public static ConsultarPerplexityResult Unauthorized(string message) => new ConsultarPerplexityUnauthorized(message);
    public static ConsultarPerplexityResult Fail(string message) => new ConsultarPerplexityFail(message);
}

public record ConsultarPerplexityOk(PerplexityResposta Resposta) : ConsultarPerplexityResult;
public record ConsultarPerplexityBadRequest(string Message) : ConsultarPerplexityResult;
public record ConsultarPerplexityUnauthorized(string Message) : ConsultarPerplexityResult;
public record ConsultarPerplexityFail(string Message) : ConsultarPerplexityResult;

public sealed class ConsultarPerplexityCommandValidator : AbstractValidator<ConsultarPerplexityCommand>
{
    public ConsultarPerplexityCommandValidator()
    {
        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.Pergunta) || (x.Mensagens is { Count: > 0 }))
            .WithMessage("Informe 'pergunta' ou a lista 'mensagens'.");
    }
}

public sealed class ConsultarPerplexityHandler : IRequestHandler<ConsultarPerplexityCommand, ConsultarPerplexityResult>
{
    private readonly IPerplexityClient _client;
    private readonly ILogger<ConsultarPerplexityHandler> _logger;

    public ConsultarPerplexityHandler(IPerplexityClient client, ILogger<ConsultarPerplexityHandler> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<ConsultarPerplexityResult> Handle(
        ConsultarPerplexityCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            var mensagens = MontarMensagens(request);
            if (mensagens.Count == 0)
            {
                _logger.LogWarning("Consulta Perplexity rejeitada: sem pergunta nem mensagens");
                return ConsultarPerplexityResult.BadRequest("Informe 'pergunta' ou a lista 'mensagens'.");
            }

            _logger.LogInformation("Consulta Perplexity iniciada mensagens={Quantidade}", mensagens.Count);
            var resposta = await _client.CompletarAsync(mensagens, cancellationToken);
            _logger.LogInformation(
                "Consulta Perplexity concluida modelo={Modelo} citacoes={Citacoes} caracteres={Tamanho}",
                resposta.Modelo,
                resposta.Citacoes.Count,
                resposta.Texto.Length);
            return ConsultarPerplexityResult.Ok(resposta);
        }
        catch (PerplexityException ex) when (ex.Kind == PerplexityErrorKind.Unauthorized)
        {
            _logger.LogWarning(ex, "Token Perplexity nao autorizado");
            return ConsultarPerplexityResult.Unauthorized(ex.Message);
        }
        catch (PerplexityException ex) when (ex.Kind == PerplexityErrorKind.InvalidRequest)
        {
            _logger.LogWarning(ex, "Consulta Perplexity invalida");
            return ConsultarPerplexityResult.BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha na consulta Perplexity");
            return ConsultarPerplexityResult.Fail(ex.Message);
        }
    }

    private static IReadOnlyList<PerplexityMensagem> MontarMensagens(ConsultarPerplexityCommand request)
    {
        if (request.Mensagens is { Count: > 0 })
        {
            return request.Mensagens
                .Where(m => !string.IsNullOrWhiteSpace(m.Conteudo))
                .Select(m => new PerplexityMensagem(
                    NormalizarPapel(m.Papel),
                    m.Conteudo.Trim()))
                .ToList();
        }

        var lista = new List<PerplexityMensagem>();
        if (!string.IsNullOrWhiteSpace(request.Contexto))
        {
            lista.Add(new PerplexityMensagem("system", request.Contexto.Trim()));
        }

        if (!string.IsNullOrWhiteSpace(request.Pergunta))
        {
            lista.Add(new PerplexityMensagem("user", request.Pergunta.Trim()));
        }

        return lista;
    }

    private static string NormalizarPapel(string? papel)
    {
        var valor = (papel ?? "user").Trim().ToLowerInvariant();
        return valor switch
        {
            "system" or "sistema" => "system",
            "assistant" or "assistente" => "assistant",
            _ => "user"
        };
    }
}
