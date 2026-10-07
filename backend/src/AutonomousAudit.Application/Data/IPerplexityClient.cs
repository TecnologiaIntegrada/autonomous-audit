using AutonomousAudit.Application;

namespace AutonomousAudit.Application.Data;

public interface IPerplexityClient
{
    Task<PerplexityResposta> CompletarAsync(
        IReadOnlyList<PerplexityMensagem> mensagens,
        CancellationToken cancellationToken = default);

    Task<PerplexityAgenteResposta> AnalisarAsync(
        PerplexityAgentePedido pedido,
        CancellationToken cancellationToken = default);
}

public sealed record PerplexityMensagem(string Papel, string Conteudo);

public sealed record PerplexityResposta(
    string Modelo,
    string Texto,
    IReadOnlyList<string> Citacoes);

public sealed record PerplexityAgentePedido(
    string Prompt,
    string? Modelo,
    byte[]? Arquivo,
    string? MimeType,
    bool ForcarImagens = false);

public sealed record PerplexityAgenteResposta(
    string Modelo,
    string Texto,
    bool Imagem,
    PerplexityUso Uso);
