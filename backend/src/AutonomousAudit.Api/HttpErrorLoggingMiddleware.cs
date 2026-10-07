namespace AutonomousAudit.Api;

public sealed class HttpErrorLoggingMiddleware
{
    private const int LimiteCaptura = 8192;
    private readonly RequestDelegate _next;

    public HttpErrorLoggingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task Invoke(HttpContext context)
    {
        var capturarRequest = DeveCapturarRequest(context.Request);
        if (capturarRequest)
        {
            context.Request.EnableBuffering();
        }

        var original = context.Response.Body;
        await using var captura = new MemoryStream();
        context.Response.Body = captura;
        try
        {
            await _next(context);
            await Completar415Vazio(context, captura);
        }
        finally
        {
            try
            {
                if (DeveRegistrar(context))
                {
                    captura.Position = 0;
                    var responseBody = await LerLimitado(captura);
                    var requestBody = capturarRequest ? await LerRequest(context) : null;
                    SeqErrorLog.Registrar(context, requestBody, responseBody);
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.ForContext<HttpErrorLoggingMiddleware>().Warning(ex, "Falha ao registrar erro HTTP no Seq");
            }

            captura.Position = 0;
            context.Response.Body = original;
            if (captura.Length > 0 && original.CanWrite)
            {
                await captura.CopyToAsync(original);
            }
        }
    }

    private static bool DeveRegistrar(HttpContext context)
    {
        var path = context.Request.Path;
        if (path.StartsWithSegments("/health") || path.StartsWithSegments("/healthz"))
        {
            return false;
        }

        return context.Response.StatusCode >= 400;
    }

    private static bool DeveCapturarRequest(HttpRequest request)
    {
        if (request.ContentLength is > LimiteCaptura or 0)
        {
            return false;
        }

        var tipo = request.ContentType ?? string.Empty;
        if (tipo.StartsWith("multipart/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return tipo.Contains("json", StringComparison.OrdinalIgnoreCase)
            || tipo.Contains("xml", StringComparison.OrdinalIgnoreCase)
            || tipo.Contains("text/", StringComparison.OrdinalIgnoreCase)
            || tipo.Contains("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(tipo);
    }

    private static async Task Completar415Vazio(HttpContext context, MemoryStream captura)
    {
        if (context.Response.StatusCode != StatusCodes.Status415UnsupportedMediaType || captura.Length > 0)
        {
            return;
        }

        var contentType = string.IsNullOrWhiteSpace(context.Request.ContentType)
            ? "(ausente)"
            : context.Request.ContentType;
        var detalhe = $"Content-Type recebido={contentType}. Este endpoint nao aceita esse tipo.";
        SeqErrorLog.Capturar(context, "media-type", detalhe);
        await Results.Problem(
                title: "Unsupported Media Type",
                detail: detalhe,
                statusCode: StatusCodes.Status415UnsupportedMediaType)
            .ExecuteAsync(context);
    }

    private static async Task<string?> LerRequest(HttpContext context)
    {
        if (!context.Request.Body.CanSeek)
        {
            return null;
        }

        context.Request.Body.Position = 0;
        using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
        var texto = await reader.ReadToEndAsync();
        context.Request.Body.Position = 0;
        return texto;
    }

    private static async Task<string?> LerLimitado(Stream stream)
    {
        if (stream.Length == 0)
        {
            return null;
        }

        var tamanho = (int)Math.Min(stream.Length, LimiteCaptura);
        var buffer = new byte[tamanho];
        var lidos = await stream.ReadAsync(buffer.AsMemory(0, tamanho));
        return lidos == 0 ? null : System.Text.Encoding.UTF8.GetString(buffer, 0, lidos);
    }
}
