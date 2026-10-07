using System.Collections;
using System.Net.Http;
using AutonomousAudit.Application.Data;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace AutonomousAudit.Application;

public static class LogExcecao
{
    private const int MaxTexto = 4096;

    private static readonly string[] ChavesSensiveis =
    [
        "senha", "password", "token", "authorization", "apikey", "api_key",
        "signingkey", "encryptionkey", "sms_auth_code", "email_auth_code"
    ];

    public static string Cadeia(Exception ex)
    {
        var partes = new List<string>();
        for (var atual = ex; atual is not null; atual = atual.InnerException)
        {
            partes.Add($"{atual.GetType().Name}: {atual.Message}");
        }

        return string.Join(" => ", partes);
    }

    public static IDisposable? IniciarEscopo(
        ILogger logger,
        Exception ex,
        params (string Chave, object? Valor)[] extra)
    {
        var props = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        foreach (var (chave, valor) in Propriedades(ex))
        {
            if (valor is not null)
            {
                props[chave] = valor;
            }
        }

        foreach (var (chave, valor) in extra)
        {
            if (valor is not null)
            {
                props[chave] = valor;
            }
        }

        return logger.BeginScope(props);
    }

    public static IReadOnlyDictionary<string, object?> Propriedades(Exception ex)
    {
        var props = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["ExceptionType"] = ex.GetType().FullName,
            ["Detail"] = Truncar(ex.Message),
            ["Cadeia"] = Cadeia(ex),
            ["HResult"] = ex.HResult
        };

        if (!string.IsNullOrWhiteSpace(ex.Source))
        {
            props["ExceptionSource"] = ex.Source;
        }

        if (ex.TargetSite is not null)
        {
            props["ExceptionTargetSite"] = $"{ex.TargetSite.DeclaringType?.FullName}.{ex.TargetSite.Name}";
        }

        if (ex.InnerException is not null)
        {
            props["InnerExceptionType"] = ex.InnerException.GetType().FullName;
            props["InnerExceptionMessage"] = Truncar(ex.InnerException.Message);
        }

        if (!string.IsNullOrWhiteSpace(ex.HelpLink))
        {
            props["HelpLink"] = ex.HelpLink;
        }

        for (var atual = ex; atual is not null; atual = atual.InnerException)
        {
            CopiarIntegracao(atual, props);
            CopiarData(atual, props);
            CopiarPropriedade(atual, props, "SqlState");
            CopiarPropriedade(atual, props, "ConstraintName");
            CopiarPropriedade(atual, props, "TableName");
            CopiarPropriedade(atual, props, "ErrorCode");
            CopiarPropriedade(atual, props, "StatusCode");
        }

        return props;
    }

    public static string Truncar(string? texto)
    {
        var valor = (texto ?? string.Empty).Trim();
        return valor.Length <= MaxTexto ? valor : valor[..MaxTexto] + "...";
    }

    private static void CopiarIntegracao(Exception atual, Dictionary<string, object?> props)
    {
        switch (atual)
        {
            case SmsDevException sms:
                props.TryAdd("Kind", sms.Kind.ToString());
                AdicionarSeTiver(props, "HttpStatus", sms.HttpStatus);
                AdicionarSeTiver(props, "ResponseBody", Truncar(sms.ResponseBody));
                break;
            case PerplexityException perplexity:
                props.TryAdd("Kind", perplexity.Kind.ToString());
                AdicionarSeTiver(props, "HttpStatus", perplexity.HttpStatus);
                AdicionarSeTiver(props, "ResponseBody", Truncar(perplexity.ResponseBody));
                break;
            case DropboxStorageException dropbox:
                props.TryAdd("Kind", dropbox.Kind.ToString());
                AdicionarSeTiver(props, "HttpStatus", dropbox.HttpStatus);
                AdicionarSeTiver(props, "ResponseBody", Truncar(dropbox.ResponseBody));
                break;
            case EmailStorageException email:
                props.TryAdd("Kind", email.Kind.ToString());
                AdicionarSeTiver(props, "HttpStatus", email.HttpStatus);
                AdicionarSeTiver(props, "ResponseBody", Truncar(email.ResponseBody));
                break;
            case HttpRequestException http:
                if (http.StatusCode is not null)
                {
                    AdicionarSeTiver(props, "HttpStatus", (int)http.StatusCode);
                }

                break;
            case ValidationException validation:
                props.TryAdd("Kind", "validacao");
                props.TryAdd(
                    "Errors",
                    validation.Errors
                        .Select(e => new
                        {
                            propriedade = e.PropertyName,
                            mensagem = e.ErrorMessage,
                            valor = Truncar(Convert.ToString(e.AttemptedValue))
                        })
                        .ToArray());
                break;
        }
    }

    private static void CopiarData(Exception ex, Dictionary<string, object?> props)
    {
        if (ex.Data.Count == 0)
        {
            return;
        }

        foreach (DictionaryEntry entrada in ex.Data)
        {
            var chave = Convert.ToString(entrada.Key);
            if (string.IsNullOrWhiteSpace(chave) || EhSensivel(chave) || props.ContainsKey(chave))
            {
                continue;
            }

            var valor = entrada.Value;
            props[chave] = valor is string texto ? Truncar(texto) : valor;
        }
    }

    private static void CopiarPropriedade(Exception ex, Dictionary<string, object?> props, string nome)
    {
        if (props.ContainsKey(nome))
        {
            return;
        }

        var propriedade = ex.GetType().GetProperty(nome);
        if (propriedade is null || !propriedade.CanRead)
        {
            return;
        }

        var valor = propriedade.GetValue(ex);
        if (valor is null)
        {
            return;
        }

        if (valor is Enum)
        {
            props[nome] = Convert.ToInt32(valor);
            return;
        }

        props[nome] = valor is string texto ? Truncar(texto) : valor;
    }

    private static void AdicionarSeTiver(Dictionary<string, object?> props, string chave, object? valor)
    {
        if (valor is null || props.ContainsKey(chave))
        {
            return;
        }

        if (valor is string texto && string.IsNullOrWhiteSpace(texto))
        {
            return;
        }

        props[chave] = valor;
    }

    private static bool EhSensivel(string chave) =>
        ChavesSensiveis.Any(s => chave.Contains(s, StringComparison.OrdinalIgnoreCase));
}
