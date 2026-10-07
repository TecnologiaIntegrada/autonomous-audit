namespace AutonomousAudit.Application.Data;

public interface ISmsDevClient
{
    Task<SmsEnvioResultado> EnviarAsync(
        string numero,
        string mensagem,
        string? referencia,
        CancellationToken cancellationToken = default);

    Task<SmsStatusResultado> ConsultarAsync(string id, CancellationToken cancellationToken = default);
}

public sealed record SmsEnvioResultado(
    string Situacao,
    string Codigo,
    string Id,
    string Numero,
    string Descricao);

public sealed record SmsStatusResultado(
    string Situacao,
    string Codigo,
    string Id,
    string? DataEnvio,
    string? Operadora,
    string Descricao);

public enum SmsDevErrorKind
{
    Unauthorized,
    InvalidRequest,
    NotFound,
    Failed
}

public sealed class SmsDevException : Exception
{
    public SmsDevErrorKind Kind { get; }
    public int? HttpStatus { get; }
    public string? ResponseBody { get; }

    public SmsDevException(
        SmsDevErrorKind kind,
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
