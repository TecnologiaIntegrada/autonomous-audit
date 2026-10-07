using System.Text.Json;
using System.Text.RegularExpressions;
using AutonomousAudit.Application;
using Serilog;

namespace AutonomousAudit.Api;

internal static class SeqErrorLog
{
    public const string ItemCategoria = "ErrorCategoria";
    public const string ItemDetail = "ErrorDetail";
    public const string ItemErrors = "ErrorErrors";
    public const string ItemException = "ErrorException";

    private const int MaxTexto = 4096;

    private static readonly Regex SegredoJson = new(
        """(?i)("(senha|password|token|authorization|sms_auth_code|email_auth_code|apikey|signingkey|encryptionkey)"\s*:\s*")[^"]*(")""",
        RegexOptions.Compiled);

    public static void Capturar(
        HttpContext context,
        string categoria,
        string? detail,
        object? errors = null,
        Exception? exception = null)
    {
        context.Items[ItemCategoria] = categoria;
        if (!string.IsNullOrWhiteSpace(detail))
        {
            context.Items[ItemDetail] = detail;
        }

        if (errors is not null)
        {
            context.Items[ItemErrors] = errors;
        }

        if (exception is not null)
        {
            context.Items[ItemException] = exception;
        }
    }

    public static void Registrar(HttpContext context, string? requestBody, string? responseBody)
    {
        var status = context.Response.StatusCode;
        var categoria = context.Items[ItemCategoria] as string ?? CategoriaPorStatus(status);
        var detail = context.Items[ItemDetail] as string;
        var errors = context.Items[ItemErrors];
        var exception = context.Items[ItemException] as Exception;
        var cadeia = exception is null ? null : LogExcecao.Cadeia(exception);

        if (string.IsNullOrWhiteSpace(detail))
        {
            detail = ExtrairDetail(responseBody);
        }

        if (string.IsNullOrWhiteSpace(detail))
        {
            detail = DetailPorStatus(status, context, requestBody);
        }

        var path = context.Request.Path.Value ?? "/";
        var method = context.Request.Method;
        if (exception is null && status >= 500)
        {
            exception = new InvalidOperationException(detail);
        }

        if (string.IsNullOrWhiteSpace(cadeia) && exception is not null)
        {
            cadeia = LogExcecao.Cadeia(exception);
        }

        var logger = Log.ForContext(typeof(SeqErrorLog));
        if (exception is not null)
        {
            foreach (var (chave, valor) in LogExcecao.Propriedades(exception))
            {
                if (valor is not null)
                {
                    logger = logger.ForContext(chave, valor, destructureObjects: valor is not string && valor is not ValueType);
                }
            }
        }

        logger = logger
            .ForContext("Categoria", categoria)
            .ForContext("StatusCode", status)
            .ForContext("Method", method)
            .ForContext("Path", path)
            .ForContext("TraceId", context.TraceIdentifier)
            .ForContext("RequestId", context.TraceIdentifier)
            .ForContext("Host", context.Request.Host.Value)
            .ForContext("Scheme", context.Request.Scheme)
            .ForContext("QueryString", context.Request.QueryString.Value)
            .ForContext("ContentType", context.Request.ContentType ?? string.Empty)
            .ForContext("ContentLength", context.Request.ContentLength ?? 0)
            .ForContext("ResponseContentType", context.Response.ContentType ?? string.Empty)
            .ForContext("Accept", context.Request.Headers.Accept.ToString())
            .ForContext("HasBearer", context.Request.Headers.Authorization.Count > 0)
            .ForContext("UserAgent", context.Request.Headers.UserAgent.ToString())
            .ForContext("Detail", detail)
            .ForContext("Cadeia", cadeia ?? string.Empty)
            .ForContext("ExceptionType", exception?.GetType().FullName)
            .ForContext("RequestBody", Redigir(requestBody))
            .ForContext("ResponseBody", Redigir(responseBody));

        if (errors is not null)
        {
            logger = logger.ForContext("Errors", errors, destructureObjects: true);
        }

        if (context.Items[RecursoAuthorization.HttpItemUsuarioId] is Guid usuarioId)
        {
            logger = logger.ForContext("UsuarioId", usuarioId);
        }

        if (context.Items[RecursoAuthorization.HttpItemUsuarioEmail] is string email)
        {
            logger = logger.ForContext("UsuarioEmail", email);
        }

        if (context.Items[RecursoAuthorization.HttpItemUsuarioNome] is string nome)
        {
            logger = logger.ForContext("UsuarioNome", nome);
        }

        const string template =
            "HTTP {Method} {Path} falhou status={StatusCode} categoria={Categoria} detail={Detail} cadeia={Cadeia} trace={TraceId}";

        if (status >= 500)
        {
            if (exception is null)
            {
                logger.Error(template, method, path, status, categoria, detail, cadeia, context.TraceIdentifier);
            }
            else
            {
                logger.Error(exception, template, method, path, status, categoria, detail, cadeia, context.TraceIdentifier);
            }

            return;
        }

        if (status == 499 || exception is OperationCanceledException)
        {
            logger.Information(template, method, path, status, categoria, detail, cadeia, context.TraceIdentifier);
            return;
        }

        if (exception is null)
        {
            logger.Warning(template, method, path, status, categoria, detail, cadeia, context.TraceIdentifier);
        }
        else
        {
            logger.Warning(exception, template, method, path, status, categoria, detail, cadeia, context.TraceIdentifier);
        }
    }

    public static string? Redigir(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return texto;
        }

        var valor = SegredoJson.Replace(texto, "$1***SECRET***$3");
        return valor.Length <= MaxTexto ? valor : valor[..MaxTexto] + "...";
    }

    private static string CategoriaPorStatus(int status) => status switch
    {
        400 => "http-400",
        401 => "nao-autorizado",
        403 => "proibido",
        404 => "nao-encontrado",
        405 => "metodo-nao-permitido",
        413 => "payload-grande",
        415 => "media-type",
        499 => "cancelado",
        >= 500 => "http-500",
        _ => "http-" + status
    };

    private static string DetailPorStatus(int status, HttpContext context, string? requestBody)
    {
        var contentType = string.IsNullOrWhiteSpace(context.Request.ContentType)
            ? "(ausente)"
            : context.Request.ContentType;
        var corpo = string.IsNullOrWhiteSpace(requestBody) ? "(vazio)" : Redigir(requestBody);
        return status switch
        {
            400 => $"400 sem ProblemDetails. Content-Type={contentType}. Corpo={corpo}",
            401 => "401 Nao autorizado. Token ausente, invalido ou expirado.",
            403 => "403 Proibido. Usuario autenticado sem o recurso exigido.",
            404 => $"404 Nao encontrado. Path={context.Request.Path.Value}",
            405 => $"405 Method Not Allowed. Method={context.Request.Method} Path={context.Request.Path.Value}",
            413 => $"413 Payload too large. ContentLength={context.Request.ContentLength}",
            415 => $"415 Unsupported Media Type. Content-Type recebido={contentType}. Esperado application/json. Corpo={corpo}",
            _ => $"HTTP {status} sem corpo de problema. Content-Type={contentType}. Corpo={corpo}"
        };
    }

    private static string? ExtrairDetail(string? responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;
            if (root.TryGetProperty("detail", out var detail) && detail.ValueKind == JsonValueKind.String)
            {
                return detail.GetString();
            }

            if (root.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String)
            {
                return title.GetString();
            }
        }
        catch (JsonException)
        {
            return Redigir(responseBody);
        }

        return null;
    }
}
