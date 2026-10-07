using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using AutonomousAudit.Application;
using AutonomousAudit.Application.Data;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AutonomousAudit.Infrastructure;

public sealed class ViaCepClient : IViaCepClient
{
    private const string Origem = "ViaCEP";
    private const string Pais = "BR";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _http;
    private readonly ILogger<ViaCepClient> _logger;

    public ViaCepClient(HttpClient http, IOptions<ViaCepOptions> options, ILogger<ViaCepClient> logger)
    {
        _http = http;
        _logger = logger;
        GarantirBase(options.Value.BaseUrl);
    }

    public async Task<EnderecoConsulta?> ConsultarPorCepAsync(string cepDigitos, CancellationToken cancellationToken = default)
    {
        var destino = MontarUri(cepDigitos, "json");
        var json = await ObterJsonAsync(destino, cancellationToken);
        if (EhErroProvedor(json))
        {
            return null;
        }

        ViaCepEnderecoDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<ViaCepEnderecoDto>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new ViaCepException(ViaCepErrorKind.Failed, "Resposta JSON invalida do ViaCEP.", ex, responseBody: Truncar(json));
        }

        if (dto is null || dto.Erro)
        {
            return null;
        }

        return Mapear(dto);
    }

    public async Task<IReadOnlyList<EnderecoConsulta>> PesquisarAsync(
        string uf,
        string cidade,
        string logradouro,
        CancellationToken cancellationToken = default)
    {
        var destino = MontarUri(uf, cidade, logradouro, "json");
        var json = await ObterJsonAsync(destino, cancellationToken);
        if (string.IsNullOrWhiteSpace(json) || json.Trim() == "[]")
        {
            return [];
        }

        if (EhErroProvedor(json))
        {
            return [];
        }

        try
        {
            var lista = JsonSerializer.Deserialize<List<ViaCepEnderecoDto>>(json, JsonOptions);
            if (lista is null)
            {
                return [];
            }

            return lista
                .Where(item => item is not null && !item.Erro)
                .Select(Mapear)
                .ToList();
        }
        catch (JsonException ex)
        {
            throw new ViaCepException(ViaCepErrorKind.Failed, "Resposta JSON invalida do ViaCEP.", ex, responseBody: Truncar(json));
        }
    }

    private async Task<string> ObterJsonAsync(Uri destino, CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, destino);
            request.Headers.Accept.ParseAdd("application/json");
            response = await _http.SendAsync(request, cancellationToken);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ViaCepException(ViaCepErrorKind.Timeout, "Tempo limite da consulta ao ViaCEP excedido.", ex);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Falha de conexao com o ViaCEP url={Url}", destino);
            throw new ViaCepException(ViaCepErrorKind.Unavailable, "ViaCEP indisponivel ou falha de conexao.", ex);
        }
        catch (Exception ex) when (ex is not ViaCepException)
        {
            _logger.LogError(ex, "Falha inesperada ao chamar o ViaCEP url={Url}", destino);
            throw new ViaCepException(ViaCepErrorKind.Failed, "Falha inesperada na consulta ao ViaCEP.", ex);
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var status = (int)response.StatusCode;

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            throw new ViaCepException(
                ViaCepErrorKind.InvalidRequest,
                "ViaCEP rejeitou os parametros da consulta.",
                httpStatus: status,
                responseBody: Truncar(body));
        }

        if (status == 429 || status == 503 || status == 502)
        {
            throw new ViaCepException(
                ViaCepErrorKind.Unavailable,
                "ViaCEP indisponivel ou com limite de requisicoes.",
                httpStatus: status,
                responseBody: Truncar(body));
        }

        if (status >= 500)
        {
            throw new ViaCepException(
                ViaCepErrorKind.Unavailable,
                "ViaCEP indisponivel.",
                httpStatus: status,
                responseBody: Truncar(body));
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new ViaCepException(
                ViaCepErrorKind.Failed,
                "Resposta inesperada do ViaCEP.",
                httpStatus: status,
                responseBody: Truncar(body));
        }

        return body;
    }

    private Uri MontarUri(params string[] segmentos)
    {
        var baseUri = _http.BaseAddress
            ?? throw new ViaCepException(ViaCepErrorKind.Failed, "Base URL do ViaCEP nao configurada.");
        GarantirBase(baseUri.AbsoluteUri);

        var relativo = string.Join("/", segmentos.Select(Uri.EscapeDataString)) + "/";
        if (!Uri.TryCreate(baseUri, relativo, out var destino))
        {
            throw new ViaCepException(ViaCepErrorKind.Failed, "Nao foi possivel montar a URL do ViaCEP.");
        }

        if (!string.Equals(destino.Host, baseUri.Host, StringComparison.OrdinalIgnoreCase)
            || !destino.AbsolutePath.StartsWith(baseUri.AbsolutePath, StringComparison.OrdinalIgnoreCase))
        {
            throw new ViaCepException(ViaCepErrorKind.Failed, "Destino fora da base configurada do ViaCEP.");
        }

        return destino;
    }

    private static void GarantirBase(string? baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl)
            || !Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new ViaCepException(ViaCepErrorKind.Failed, "ViaCep:BaseUrl deve ser uma URL HTTPS.");
        }
    }

    private static bool EhErroProvedor(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (!doc.RootElement.TryGetProperty("erro", out var erro))
            {
                return false;
            }

            return erro.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.String => string.Equals(erro.GetString(), "true", StringComparison.OrdinalIgnoreCase),
                _ => false
            };
        }
        catch (JsonException)
        {
            throw new ViaCepException(ViaCepErrorKind.Failed, "Resposta JSON invalida do ViaCEP.", responseBody: Truncar(json));
        }
    }

    private static EnderecoConsulta Mapear(ViaCepEnderecoDto dto) =>
        new(
            CepBrasil.Formatado(dto.Cep),
            Texto(dto.Logradouro),
            Texto(dto.Complemento),
            Texto(dto.Unidade),
            Texto(dto.Bairro),
            Texto(dto.Localidade),
            Texto(dto.Uf).ToUpperInvariant(),
            Texto(dto.Estado),
            Texto(dto.Regiao),
            Texto(dto.Ibge),
            Texto(dto.Gia),
            Texto(dto.Ddd),
            Texto(dto.Siafi),
            Pais,
            Origem);

    private static string Texto(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? string.Empty : valor.Trim();

    private static string Truncar(string? texto)
    {
        if (string.IsNullOrEmpty(texto))
        {
            return string.Empty;
        }

        return texto.Length <= 500 ? texto : texto[..500] + "...";
    }

    private sealed class ViaCepEnderecoDto
    {
        [JsonPropertyName("cep")]
        public string? Cep { get; set; }

        [JsonPropertyName("logradouro")]
        public string? Logradouro { get; set; }

        [JsonPropertyName("complemento")]
        public string? Complemento { get; set; }

        [JsonPropertyName("unidade")]
        public string? Unidade { get; set; }

        [JsonPropertyName("bairro")]
        public string? Bairro { get; set; }

        [JsonPropertyName("localidade")]
        public string? Localidade { get; set; }

        [JsonPropertyName("uf")]
        public string? Uf { get; set; }

        [JsonPropertyName("estado")]
        public string? Estado { get; set; }

        [JsonPropertyName("regiao")]
        public string? Regiao { get; set; }

        [JsonPropertyName("ibge")]
        public string? Ibge { get; set; }

        [JsonPropertyName("gia")]
        public string? Gia { get; set; }

        [JsonPropertyName("ddd")]
        public string? Ddd { get; set; }

        [JsonPropertyName("siafi")]
        public string? Siafi { get; set; }

        [JsonConverter(typeof(ViaCepErroConverter))]
        [JsonPropertyName("erro")]
        public bool Erro { get; set; }
    }

    private sealed class ViaCepErroConverter : JsonConverter<bool>
    {
        public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            reader.TokenType switch
            {
                JsonTokenType.True => true,
                JsonTokenType.False => false,
                JsonTokenType.String => string.Equals(reader.GetString(), "true", StringComparison.OrdinalIgnoreCase),
                JsonTokenType.Number => reader.TryGetInt32(out var n) && n != 0,
                _ => false
            };

        public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options) =>
            writer.WriteBooleanValue(value);
    }
}
