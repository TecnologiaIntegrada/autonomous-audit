namespace AutonomousAudit.Application.Data;

public enum PerplexityErrorKind
{
    Unauthorized,
    InvalidRequest,
    Failed
}

public sealed class PerplexityException : Exception
{
    public PerplexityErrorKind Kind { get; }
    public int? HttpStatus { get; }
    public string? ResponseBody { get; }

    public PerplexityException(
        PerplexityErrorKind kind,
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
