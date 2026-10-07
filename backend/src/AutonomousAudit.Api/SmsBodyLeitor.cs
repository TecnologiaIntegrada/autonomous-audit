using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;

namespace AutonomousAudit.Api;

internal static class SmsBodyLeitor
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static async Task<(EnviarSmsBody? Body, string Origem, string? Erro)> LerAsync(
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Body.CanSeek)
        {
            request.Body.Position = 0;
        }

        using var reader = new StreamReader(request.Body, leaveOpen: true);
        var bruto = (await reader.ReadToEndAsync(cancellationToken)).Trim();
        if (request.Body.CanSeek)
        {
            request.Body.Position = 0;
        }

        if (string.IsNullOrWhiteSpace(bruto))
        {
            return (null, "vazio", "Corpo vazio. Envie JSON {\"numero\":\"11988887777\",\"mensagem\":\"texto\"}.");
        }

        if (TentarJson(bruto, out var jsonBody) && jsonBody is not null)
        {
            return (jsonBody, "json", null);
        }

        if (EhFormulario(request.ContentType) && TentarFormulario(bruto, out var formBody) && formBody is not null)
        {
            return (formBody, "form", null);
        }

        if (TentarObjetoSolto(bruto, out var solto) && solto is not null)
        {
            return (solto, "objeto-solto", null);
        }

        if (TentarFormulario(bruto, out var queryBody) && queryBody is not null)
        {
            return (queryBody, "query", null);
        }

        return (null, "invalido",
            "JSON invalido. No PowerShell use aspas simples no -d: -d '{\"numero\":\"+5511999998888\",\"mensagem\":\"teste\"}'.");
    }

    private static bool TentarJson(string bruto, out EnviarSmsBody? body)
    {
        body = null;
        if (!bruto.StartsWith('{') || !bruto.Contains('"'))
        {
            return false;
        }

        try
        {
            body = JsonSerializer.Deserialize<EnviarSmsBody>(bruto, Json);
            return body is not null;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TentarFormulario(string bruto, out EnviarSmsBody? body)
    {
        body = null;
        if (!bruto.Contains('=') || bruto.StartsWith('{'))
        {
            return false;
        }

        var pares = QueryHelpers.ParseQuery(bruto);
        var numero = pares.TryGetValue("numero", out var n) ? n.ToString() : null;
        var mensagem = pares.TryGetValue("mensagem", out var m) ? m.ToString() : null;
        var referencia = pares.TryGetValue("referencia", out var r) ? r.ToString() : null;
        if (string.IsNullOrWhiteSpace(numero) && string.IsNullOrWhiteSpace(mensagem))
        {
            return false;
        }

        body = new EnviarSmsBody(numero, mensagem, referencia);
        return true;
    }

    private static bool TentarObjetoSolto(string bruto, out EnviarSmsBody? body)
    {
        body = null;
        var texto = bruto.Trim();
        if (texto.Length < 3 || texto[0] != '{' || texto[^1] != '}')
        {
            return false;
        }

        texto = texto[1..^1].Trim();
        var campos = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var i = 0;
        while (i < texto.Length)
        {
            i = PularEspacos(texto, i);
            if (i >= texto.Length)
            {
                break;
            }

            var chave = LerChave(texto, ref i);
            if (string.IsNullOrWhiteSpace(chave))
            {
                return false;
            }

            i = PularEspacos(texto, i);
            if (i >= texto.Length || texto[i] != ':')
            {
                return false;
            }

            i++;
            i = PularEspacos(texto, i);
            var valor = LerValor(texto, ref i);
            campos[chave] = valor;
            i = PularEspacos(texto, i);
            if (i < texto.Length && texto[i] == ',')
            {
                i++;
            }
        }

        if (campos.Count == 0)
        {
            return false;
        }

        campos.TryGetValue("numero", out var numero);
        campos.TryGetValue("mensagem", out var mensagem);
        campos.TryGetValue("referencia", out var referencia);
        body = new EnviarSmsBody(numero, mensagem, referencia);
        return !string.IsNullOrWhiteSpace(numero) || !string.IsNullOrWhiteSpace(mensagem);
    }

    private static int PularEspacos(string texto, int i)
    {
        while (i < texto.Length && char.IsWhiteSpace(texto[i]))
        {
            i++;
        }

        return i;
    }

    private static string LerChave(string texto, ref int i)
    {
        if (i < texto.Length && (texto[i] == '"' || texto[i] == '\''))
        {
            return LerEntreAspas(texto, ref i);
        }

        var inicio = i;
        while (i < texto.Length && texto[i] != ':' && !char.IsWhiteSpace(texto[i]))
        {
            i++;
        }

        return texto[inicio..i];
    }

    private static string LerValor(string texto, ref int i)
    {
        if (i < texto.Length && (texto[i] == '"' || texto[i] == '\''))
        {
            return LerEntreAspas(texto, ref i);
        }

        var inicio = i;
        while (i < texto.Length && texto[i] != ',')
        {
            i++;
        }

        return texto[inicio..i].Trim();
    }

    private static string LerEntreAspas(string texto, ref int i)
    {
        var aspas = texto[i];
        i++;
        var inicio = i;
        while (i < texto.Length && texto[i] != aspas)
        {
            if (texto[i] == '\\' && i + 1 < texto.Length)
            {
                i += 2;
                continue;
            }

            i++;
        }

        var valor = texto[inicio..Math.Min(i, texto.Length)];
        if (i < texto.Length && texto[i] == aspas)
        {
            i++;
        }

        return valor;
    }

    private static bool EhFormulario(string? contentType) =>
        !string.IsNullOrWhiteSpace(contentType)
        && contentType.Contains("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase);
}
