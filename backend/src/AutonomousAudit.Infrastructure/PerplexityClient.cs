using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AutonomousAudit.Application;
using AutonomousAudit.Application.Data;
using AutonomousAudit.Application.RequestHandlers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AutonomousAudit.Infrastructure;

public sealed class PerplexityClient : IPerplexityClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _http;
    private readonly PerplexityOptions _options;
    private readonly IPerplexityDocumentoPreparador _preparador;
    private readonly ILogger<PerplexityClient> _logger;

    public PerplexityClient(
        HttpClient http,
        IOptions<PerplexityOptions> options,
        IPerplexityDocumentoPreparador preparador,
        ILogger<PerplexityClient> logger)
    {
        _http = http;
        _options = options.Value;
        _preparador = preparador;
        _logger = logger;
    }

    public async Task<PerplexityResposta> CompletarAsync(
        IReadOnlyList<PerplexityMensagem> mensagens,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            var ex = new PerplexityException(
                PerplexityErrorKind.Unauthorized,
                "Perplexity nao configurado. Informe Perplexity:ApiKey ou PERPLEXITY_API_KEY.");
            ex.Data["HasApiKey"] = false;
            ex.Data["ConfigKeys"] = "Perplexity:ApiKey, PERPLEXITY_API_KEY";
            throw ex;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        request.Content = JsonContent.Create(new
        {
            model = string.IsNullOrWhiteSpace(_options.Model) ? "sonar" : _options.Model,
            messages = mensagens.Select(m => new { role = m.Papel, content = m.Conteudo }).ToArray()
        });

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha de rede na API Perplexity");
            throw new PerplexityException(PerplexityErrorKind.Failed, ex.Message, ex);
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var status = (int)response.StatusCode;
        var bodyLog = LogExcecao.Truncar(body);
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            var ex = new PerplexityException(
                PerplexityErrorKind.Unauthorized,
                "Token Perplexity invalido ou expirado.",
                httpStatus: status,
                responseBody: bodyLog);
            _logger.LogWarning(ex, "API Perplexity recusou o token status={Status} body={Body}", status, bodyLog);
            throw ex;
        }

        if (status is >= 400 and < 500)
        {
            var ex = new PerplexityException(
                PerplexityErrorKind.InvalidRequest,
                MensagemErro(body),
                httpStatus: status,
                responseBody: bodyLog);
            _logger.LogWarning(ex, "API Perplexity retornou {Status} body={Body}", status, bodyLog);
            throw ex;
        }

        if (!response.IsSuccessStatusCode)
        {
            var ex = new PerplexityException(
                PerplexityErrorKind.Failed,
                MensagemErro(body),
                httpStatus: status,
                responseBody: bodyLog);
            _logger.LogError(ex, "API Perplexity retornou {Status} body={Body}", status, bodyLog);
            throw ex;
        }

        var parsed = JsonSerializer.Deserialize<PerplexityApiResponse>(body, JsonOptions)
            ?? throw new PerplexityException(PerplexityErrorKind.Failed, "Resposta vazia do Perplexity.");

        var texto = parsed.Choices?.FirstOrDefault()?.Message?.Content ?? string.Empty;
        return new PerplexityResposta(
            parsed.Model ?? _options.Model,
            texto,
            parsed.Citations ?? []);
    }

    public async Task<PerplexityAgenteResposta> AnalisarAsync(
        PerplexityAgentePedido pedido,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new PerplexityException(
                PerplexityErrorKind.Unauthorized,
                "Perplexity nao configurado. Informe Perplexity:ApiKey ou PERPLEXITY_API_KEY.");
        }

        var modelo = PerplexityModelos.NormalizarAgente(
            string.IsNullOrWhiteSpace(pedido.Modelo) ? _options.AgentModel : pedido.Modelo);
        var prepararMs = Stopwatch.StartNew();
        _logger.LogInformation(
            "OCR Perplexity preparar iniciado bytes={Bytes} mime={Mime} forcarImagens={ForcarImagens} modelo={Modelo}",
            pedido.Arquivo?.Length ?? 0,
            pedido.MimeType,
            pedido.ForcarImagens,
            modelo);
        PerplexityDocumentoPreparacao documento;
        try
        {
            documento = _preparador.Preparar(pedido.Arquivo, pedido.MimeType, pedido.ForcarImagens);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "OCR Perplexity preparar falhou bytes={Bytes} mime={Mime} forcarImagens={ForcarImagens} duracaoMs={DuracaoMs} tipo={Tipo} cadeia={Cadeia}",
                pedido.Arquivo?.Length ?? 0,
                pedido.MimeType,
                pedido.ForcarImagens,
                prepararMs.ElapsedMilliseconds,
                ex.GetType().Name,
                LogExcecao.Cadeia(ex));
            throw;
        }

        _logger.LogInformation(
            "OCR Perplexity documento preparado imagens={Imagens} textoPdfChars={TextoPdfChars} bytesImagens={BytesImagens} duracaoMs={DuracaoMs} modelo={Modelo}",
            documento.Imagens.Count,
            documento.TextoPdf?.Length ?? 0,
            documento.Imagens.Sum(i => i.Bytes.Length),
            prepararMs.ElapsedMilliseconds,
            modelo);
        var prompt = MontarPrompt(pedido.Prompt, documento.TextoPdf);
        var content = new List<object> { new { type = "input_text", text = prompt } };
        long tokensImagemEstimados = 0;
        foreach (var imagem in documento.Imagens)
        {
            var mime = string.IsNullOrWhiteSpace(imagem.MimeType) ? "image/jpeg" : imagem.MimeType;
            content.Add(new
            {
                type = "input_image",
                image_url = $"data:{mime};base64,{Convert.ToBase64String(imagem.Bytes)}"
            });
            tokensImagemEstimados += PerplexityCustoEstimado.TokensImagem(imagem.Largura, imagem.Altura);
        }

        var payload = new Dictionary<string, object?>
        {
            ["model"] = modelo,
            ["max_steps"] = 1,
            ["tools"] = Array.Empty<object>(),
            ["input"] = new object[]
            {
                new { role = "user", content }
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "v1/agent");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        request.Content = JsonContent.Create(payload);

        HttpResponseMessage response;
        var httpMs = Stopwatch.StartNew();
        _logger.LogInformation(
            "OCR Perplexity HTTP iniciado modelo={Modelo} imagens={Imagens} timeoutSeconds={TimeoutSeconds}",
            modelo,
            documento.Imagens.Count,
            _options.TimeoutSeconds);
        try
        {
            response = await _http.SendAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Falha de rede na Agent API Perplexity modelo={Modelo} imagens={Imagens} duracaoMs={DuracaoMs} tipo={Tipo} cadeia={Cadeia}",
                modelo,
                documento.Imagens.Count,
                httpMs.ElapsedMilliseconds,
                ex.GetType().Name,
                LogExcecao.Cadeia(ex));
            throw new PerplexityException(PerplexityErrorKind.Failed, ex.Message, ex);
        }

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var status = (int)response.StatusCode;
            var bodyLog = LogExcecao.Truncar(body);
            _logger.LogInformation(
                "OCR Perplexity HTTP concluido modelo={Modelo} status={Status} duracaoMs={DuracaoMs} bodyChars={BodyChars}",
                modelo,
                status,
                httpMs.ElapsedMilliseconds,
                body?.Length ?? 0);

            if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
            {
                var ex = new PerplexityException(
                    PerplexityErrorKind.Unauthorized,
                    "Token Perplexity invalido ou sem permissao.",
                    httpStatus: status,
                    responseBody: bodyLog);
                _logger.LogWarning(ex, "Agent API Perplexity recusou a chave status={Status} body={Body}", status, bodyLog);
                throw ex;
            }

            if (status is >= 400 and < 500)
            {
                var ex = new PerplexityException(
                    PerplexityErrorKind.InvalidRequest,
                    MensagemErro(body),
                    httpStatus: status,
                    responseBody: bodyLog);
                _logger.LogWarning(ex, "Agent API Perplexity retornou {Status} body={Body}", status, bodyLog);
                throw ex;
            }

            if (!response.IsSuccessStatusCode)
            {
                var ex = new PerplexityException(
                    PerplexityErrorKind.Failed,
                    MensagemErro(body),
                    httpStatus: status,
                    responseBody: bodyLog);
                _logger.LogError(ex, "Agent API Perplexity retornou {Status} body={Body}", status, bodyLog);
                throw ex;
            }

            var parsed = JsonSerializer.Deserialize<PerplexityAgentResponse>(body, JsonOptions)
                ?? throw new PerplexityException(PerplexityErrorKind.Failed, "Resposta vazia do Perplexity.");

            var texto = ExtrairTexto(parsed);
            if (string.IsNullOrWhiteSpace(texto))
            {
                throw new PerplexityException(PerplexityErrorKind.Failed, "Perplexity nao devolveu texto.");
            }

            var promptTokens = parsed.Usage?.InputTokens ?? 0;
            var outputTokens = parsed.Usage?.OutputTokens ?? 0;
            var totalTokens = parsed.Usage?.TotalTokens ?? 0;
            if (totalTokens <= 0)
            {
                totalTokens = promptTokens + outputTokens;
            }

            if (promptTokens <= 0 && tokensImagemEstimados > 0)
            {
                promptTokens = tokensImagemEstimados;
                if (totalTokens <= 0)
                {
                    totalTokens = promptTokens + outputTokens;
                }
            }

            _logger.LogInformation(
                "OCR Perplexity resposta ok modelo={Modelo} textoChars={TextoChars} promptTokens={PromptTokens} outputTokens={OutputTokens} totalTokens={TotalTokens}",
                parsed.Model ?? modelo,
                texto.Trim().Length,
                promptTokens,
                outputTokens,
                totalTokens);

            return new PerplexityAgenteResposta(
                parsed.Model ?? modelo,
                texto.Trim(),
                documento.Imagens.Count > 0,
                new PerplexityUso(promptTokens, outputTokens, totalTokens, parsed.Usage?.Cost?.TotalCost));
        }
    }

    private static string MontarPrompt(string prompt, string? textoPdf)
    {
        var basePrompt = string.IsNullOrWhiteSpace(prompt)
            ? "Transcreva todo o texto visivel neste arquivo. Mantenha a ordem de leitura."
            : prompt.Trim();
        if (string.IsNullOrWhiteSpace(textoPdf))
        {
            return basePrompt;
        }

        return basePrompt + "\n\nTexto extraido do PDF:\n" + textoPdf.Trim();
    }

    private static string ExtrairTexto(PerplexityAgentResponse parsed)
    {
        if (!string.IsNullOrWhiteSpace(parsed.OutputText))
        {
            return parsed.OutputText;
        }

        if (parsed.Output is null)
        {
            return string.Empty;
        }

        foreach (var item in parsed.Output)
        {
            if (item.Content is null)
            {
                continue;
            }

            foreach (var parte in item.Content)
            {
                if (!string.IsNullOrWhiteSpace(parte.Text))
                {
                    return parte.Text;
                }
            }
        }

        return string.Empty;
    }

    private static string MensagemErro(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var error))
            {
                if (error.ValueKind == JsonValueKind.Object && error.TryGetProperty("message", out var message))
                {
                    return message.GetString() ?? body;
                }

                return error.ToString();
            }
        }
        catch (JsonException)
        {
        }

        return string.IsNullOrWhiteSpace(body) ? "Falha na API Perplexity." : body;
    }

    private sealed class PerplexityApiResponse
    {
        [JsonPropertyName("model")]
        public string? Model { get; set; }

        [JsonPropertyName("choices")]
        public List<PerplexityChoice>? Choices { get; set; }

        [JsonPropertyName("citations")]
        public List<string>? Citations { get; set; }
    }

    private sealed class PerplexityChoice
    {
        [JsonPropertyName("message")]
        public PerplexityApiMessage? Message { get; set; }
    }

    private sealed class PerplexityApiMessage
    {
        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }

    private sealed class PerplexityAgentResponse
    {
        [JsonPropertyName("model")]
        public string? Model { get; set; }

        [JsonPropertyName("output_text")]
        public string? OutputText { get; set; }

        [JsonPropertyName("output")]
        public List<PerplexityAgentOutputItem>? Output { get; set; }

        [JsonPropertyName("usage")]
        public PerplexityAgentUsage? Usage { get; set; }
    }

    private sealed class PerplexityAgentOutputItem
    {
        [JsonPropertyName("content")]
        public List<PerplexityAgentContent>? Content { get; set; }
    }

    private sealed class PerplexityAgentContent
    {
        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }

    private sealed class PerplexityAgentUsage
    {
        [JsonPropertyName("input_tokens")]
        public long InputTokens { get; set; }

        [JsonPropertyName("output_tokens")]
        public long OutputTokens { get; set; }

        [JsonPropertyName("total_tokens")]
        public long TotalTokens { get; set; }

        [JsonPropertyName("cost")]
        public PerplexityAgentCost? Cost { get; set; }
    }

    private sealed class PerplexityAgentCost
    {
        [JsonPropertyName("total_cost")]
        public decimal TotalCost { get; set; }
    }
}
