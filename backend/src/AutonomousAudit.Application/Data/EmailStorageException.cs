namespace AutonomousAudit.Application.Data;

public enum EmailErrorKind
{
    NotFound,
    Unauthorized,
    InvalidRequest,
    Failed
}

public sealed class EmailStorageException : Exception
{
    public EmailErrorKind Kind { get; }
    public int? HttpStatus { get; }
    public string? ResponseBody { get; }

    public EmailStorageException(
        EmailErrorKind kind,
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
