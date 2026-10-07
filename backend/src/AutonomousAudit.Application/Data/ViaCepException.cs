namespace AutonomousAudit.Application.Data;

public enum ViaCepErrorKind
{
    InvalidRequest,
    NotFound,
    Failed,
    Unavailable,
    Timeout
}

public sealed class ViaCepException : Exception
{
    public ViaCepErrorKind Kind { get; }
    public int? HttpStatus { get; }
    public string? ResponseBody { get; }

    public ViaCepException(
        ViaCepErrorKind kind,
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
