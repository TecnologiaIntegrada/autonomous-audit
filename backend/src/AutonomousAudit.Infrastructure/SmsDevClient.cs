using System.Diagnostics;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using AutonomousAudit.Application.Data;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AutonomousAudit.Infrastructure;

public sealed class SmsDevClient : ISmsDevClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly Regex ChaveQuery = new(
        @"(?i)([?&]key=)[^&\s""]+",
        RegexOptions.Compiled);

    private readonly HttpClient _http;
    private readonly SmsDevOptions _options;
    private readonly ILogger<SmsDevClient> _logger;

    public SmsDevClient(HttpClient http, IOptions<SmsDevOptions> options, ILogger<SmsDevClient> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<SmsEnvioResultado> EnviarAsync(
        string numero,
        string mensagem,
        string? referencia,
        CancellationToken cancellationToken = default)
    {
        GarantirChave();

        var query = new Dictionary<string, string?>
        {
            ["key"] = _options.ApiKey,
            ["type"] = _options.Type.ToString(),
            ["number"] = numero,
            ["msg"] = mensagem
        };
        if (!string.IsNullOrWhiteSpace(referencia))
        {
            query["refer"] = referencia.Trim();
        }

        var json = await GetJsonAsync("send", query, cancellationToken);
        var parsed = JsonSerializer.Deserialize<SmsDevEnvioResponse>(json, JsonOptions)
            ?? throw new SmsDevException(
                SmsDevErrorKind.Failed,
                "Resposta vazia da SMSDev no envio.",
                responseBody: Truncar(json));

        var resultado = new SmsEnvioResultado(
            parsed.Situacao ?? "ERRO",
            Valor(parsed.Codigo),
            Valor(parsed.Id),
            parsed.Number ?? numero,
            parsed.Descricao ?? string.Empty);

        var ok = resultado.Situacao.Equals("OK", StringComparison.OrdinalIgnoreCase);
        _logger.Log(
            ok ? LogLevel.Information : LogLevel.Warning,
            "SMSDev envio situacao={Situacao} codigo={CodigoSmsDev} id={Id} numero={NumeroSufixo} type={Type} referencia={Referencia} descricao={Descricao} body={Body}",
            resultado.Situacao,
            resultado.Codigo,
            resultado.Id,
            Sufixo(numero),
            _options.Type,
            referencia,
            resultado.Descricao,
            Truncar(json));

        return resultado;
    }

    public async Task<SmsStatusResultado> ConsultarAsync(string id, CancellationToken cancellationToken = default)
    {
        GarantirChave();

        var json = await GetJsonAsync(
            "dlr",
            new Dictionary<string, string?>
            {
                ["key"] = _options.ApiKey,
                ["id"] = id
            },
            cancellationToken);

        if (string.IsNullOrWhiteSpace(json))
        {
            throw new SmsDevException(SmsDevErrorKind.NotFound, "SMS nao encontrado na SMSDev.");
        }

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement.ValueKind == JsonValueKind.Array
            ? doc.RootElement.EnumerateArray().FirstOrDefault()
            : doc.RootElement;

        if (root.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            throw new SmsDevException(SmsDevErrorKind.NotFound, "SMS nao encontrado na SMSDev.", responseBody: Truncar(json));
        }

        var parsed = root.Deserialize<SmsDevStatusResponse>(JsonOptions)
            ?? throw new SmsDevException(
                SmsDevErrorKind.Failed,
                "Resposta vazia da SMSDev no status.",
                responseBody: Truncar(json));

        var status = new SmsStatusResultado(
            parsed.Situacao ?? "ERRO",
            Valor(parsed.Codigo),
            string.IsNullOrWhiteSpace(Valor(parsed.Id)) ? id : Valor(parsed.Id),
            parsed.DataEnvio,
            parsed.Operadora,
            parsed.Descricao ?? string.Empty);

        _logger.LogInformation(
            "SMSDev status id={Id} situacao={Situacao} codigo={CodigoSmsDev} operadora={Operadora} descricao={Descricao} body={Body}",
            status.Id,
            status.Situacao,
            status.Codigo,
            status.Operadora,
            status.Descricao,
            Truncar(json));

        return status;
    }

    private void GarantirChave()
    {
        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return;
        }

        var ex = new SmsDevException(
            SmsDevErrorKind.Unauthorized,
            "SMSDev nao configurado. Informe SmsDev:ApiKey ou SMSDEV_API_KEY.");
        ex.Data["HasApiKey"] = false;
        ex.Data["ConfigKeys"] = "SmsDev:ApiKey, SMSDEV_API_KEY";
        throw ex;
    }

    private async Task<string> GetJsonAsync(
        string path,
        Dictionary<string, string?> query,
        CancellationToken cancellationToken)
    {
        var url = path + QueryString(query);
        var urlLog = RedigirChave(url);
        var sw = Stopwatch.StartNew();
        HttpResponseMessage response;
        try
        {
            response = await _http.GetAsync(url, cancellationToken);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(
                ex,
                "Falha de rede na SMSDev path={Path} url={Url} elapsedMs={ElapsedMs} tipo={Tipo}",
                path,
                urlLog,
                sw.ElapsedMilliseconds,
                ex.GetType().Name);
            throw new SmsDevException(SmsDevErrorKind.Failed, ex.Message, ex);
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        sw.Stop();
        var bodyLog = Truncar(RedigirChave(body));
        var status = (int)response.StatusCode;
        _logger.Log(
            response.IsSuccessStatusCode ? LogLevel.Information : LogLevel.Warning,
            "SMSDev HTTP status={Status} path={Path} url={Url} elapsedMs={ElapsedMs} contentType={ContentType} body={Body}",
            status,
            path,
            urlLog,
            sw.ElapsedMilliseconds,
            response.Content.Headers.ContentType?.ToString(),
            bodyLog);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new SmsDevException(
                SmsDevErrorKind.Unauthorized,
                "Token SMSDev invalido.",
                httpStatus: status,
                responseBody: bodyLog);
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new SmsDevException(
                SmsDevErrorKind.NotFound,
                "Recurso SMSDev nao encontrado.",
                httpStatus: status,
                responseBody: bodyLog);
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new SmsDevException(
                SmsDevErrorKind.Failed,
                $"SMSDev retornou {status}: {bodyLog}",
                httpStatus: status,
                responseBody: bodyLog);
        }

        return body;
    }

    private static string QueryString(Dictionary<string, string?> query)
    {
        var parts = query
            .Where(kv => !string.IsNullOrWhiteSpace(kv.Value))
            .Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value!)}");
        return "?" + string.Join("&", parts);
    }

    private static string Valor(JsonElement? element)
    {
        if (element is null || element.Value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return string.Empty;
        }

        return element.Value.ValueKind == JsonValueKind.String
            ? element.Value.GetString() ?? string.Empty
            : element.Value.ToString();
    }

    private static string Sufixo(string numero) =>
        numero.Length <= 4 ? "****" : "****" + numero[^4..];

    private static string Truncar(string? texto)
    {
        var valor = (texto ?? string.Empty).Trim();
        return valor.Length <= 1500 ? valor : valor[..1500] + "...";
    }

    private static string RedigirChave(string texto) =>
        ChaveQuery.Replace(texto ?? string.Empty, "$1***SECRET***");

    private sealed class SmsDevEnvioResponse
    {
        [JsonPropertyName("situacao")]
        public string? Situacao { get; set; }

        [JsonPropertyName("codigo")]
        public JsonElement Codigo { get; set; }

        [JsonPropertyName("id")]
        public JsonElement Id { get; set; }

        [JsonPropertyName("number")]
        public string? Number { get; set; }

        [JsonPropertyName("descricao")]
        public string? Descricao { get; set; }
    }

    private sealed class SmsDevStatusResponse
    {
        [JsonPropertyName("situacao")]
        public string? Situacao { get; set; }

        [JsonPropertyName("codigo")]
        public JsonElement Codigo { get; set; }

        [JsonPropertyName("id")]
        public JsonElement Id { get; set; }

        [JsonPropertyName("data_envio")]
        public string? DataEnvio { get; set; }

        [JsonPropertyName("operadora")]
        public string? Operadora { get; set; }

        [JsonPropertyName("descricao")]
        public string? Descricao { get; set; }
    }
}
