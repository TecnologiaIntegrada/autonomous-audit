namespace AutonomousAudit.Application.Data;

public interface IArquivoEventoPublisher
{
    Task<ArquivoEventoPublicado> PublicarRecebidoAsync(
        ArquivoRecebidoEvento evento,
        CancellationToken cancellationToken = default);

    Task<ArquivoEventoPublicado> PublicarDropboxAsync(
        DropboxDispatchEvento evento,
        CancellationToken cancellationToken = default);

    Task<ArquivoEventoPublicado> PublicarPerplexityPromptAsync(
        PerplexityPromptEvento evento,
        CancellationToken cancellationToken = default);

    Task<ArquivoEventoPublicado> PublicarPerplexityArquivoAsync(
        PerplexityArquivoEvento evento,
        CancellationToken cancellationToken = default);

    Task<ArquivoEventoPublicado> PublicarMailAsync(
        MailEnvioEvento evento,
        CancellationToken cancellationToken = default);

    Task<ArquivoEventoPublicado> PublicarCompraProcessarAsync(
        CompraProcessarEvento evento,
        CancellationToken cancellationToken = default);
}

public sealed record ArquivoRecebidoEvento(
    Guid Id,
    string NomeArquivo,
    long TamanhoBytes,
    decimal TamanhoMb,
    DateTimeOffset DataHora,
    string UsuarioRemetente,
    string DestinoDropbox);

public sealed record DropboxDispatchEvento(
    Guid TransacaoId,
    Guid ArquivoRecebidoId,
    string CaminhoDropbox,
    string NomeArquivo,
    string NomeOriginal,
    string CaminhoLocal);

public sealed record ArquivoEventoPublicado(string Subject, string Stream, ulong Sequencia);

public sealed record PerplexityPromptEvento(Guid Id);

public sealed record PerplexityArquivoEvento(Guid Id);

public sealed record MailEnvioEvento(
    Guid Id,
    string Corpo,
    bool Html,
    string? CaminhoAnexo,
    string? NomeAnexo);

public sealed record CompraProcessarEvento(Guid CompraId, Guid MensagemId);
