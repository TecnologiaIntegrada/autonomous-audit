namespace AutonomousAudit.Application.Data;

public interface IDropboxDocumentos
{
    Task<DropboxContaAtual> ObterContaAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DropboxEntrada>> ListarOuBuscarAsync(
        string? consulta,
        string? caminho,
        CancellationToken cancellationToken = default);

    Task<DropboxEntrada> EnviarAsync(
        string caminhoDestino,
        Stream conteudo,
        CancellationToken cancellationToken = default);

    Task<DropboxEntrada> ExcluirAsync(string caminho, CancellationToken cancellationToken = default);

    Task<DropboxEntrada> MoverAsync(
        string caminhoOrigem,
        string caminhoDestino,
        CancellationToken cancellationToken = default);

    Task<string> ObterLinkTemporarioAsync(string caminho, CancellationToken cancellationToken = default);

    Task<byte[]> BaixarAsync(string caminho, CancellationToken cancellationToken = default);
}

public sealed record DropboxContaAtual(
    string Email,
    string Nome,
    string TipoConta,
    string Pais,
    string Locale,
    string AccountId,
    bool EmailVerificado);

public sealed record DropboxEntrada(
    string Nome,
    string Caminho,
    string? Id,
    long? TamanhoBytes,
    DateTimeOffset? ModificadoEm,
    string Tipo);

public enum DropboxErrorKind
{
    NotFound,
    Unauthorized,
    Forbidden,
    InvalidRequest,
    Failed
}

public sealed class DropboxStorageException : Exception
{
    public DropboxErrorKind Kind { get; }
    public int? HttpStatus { get; }
    public string? ResponseBody { get; }

    public DropboxStorageException(
        DropboxErrorKind kind,
        string message,
        Exception? inner = null,
        int? httpStatus = null,
        string? responseBody = null)
        : base(message, inner)
    {
        Kind = kind;
        HttpStatus = httpStatus;
        ResponseBody = responseBody;
    }
}
