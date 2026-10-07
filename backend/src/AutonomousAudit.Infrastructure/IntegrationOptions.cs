using Microsoft.Extensions.Configuration;

namespace AutonomousAudit.Infrastructure;

public sealed class TitanSmtpOptions
{
    public const string SectionName = "Smtp:Titan";

    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 465;
    public string TlsMode { get; set; } = "ssl";
    public string User { get; set; } = string.Empty;
    public string From { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public sealed class PerplexityOptions
{
    public const string SectionName = "Perplexity";

    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "sonar";
    public string AgentModel { get; set; } = "openai/gpt-5-mini";
    public string BaseUrl { get; set; } = "https://api.perplexity.ai/";
    public int TimeoutSeconds { get; set; } = 300;
    public int MaxPaginasPdf { get; set; } = 15;
    public int DpiPdf { get; set; } = 144;
    public int MaxLadoImagemPx { get; set; } = 1600;
    public int JpegQuality { get; set; } = 80;
    public int MinCharsTextoPdf { get; set; } = 80;
    public int RasterizarTimeoutSeconds { get; set; } = 90;
    public decimal PrecoInputUsdPorMilhao { get; set; } = 0.25m;
    public decimal PrecoOutputUsdPorMilhao { get; set; } = 2.00m;
    public decimal TaxaUsdParaBrl { get; set; } = 5.45m;
}

public sealed class SmsDevOptions
{
    public const string SectionName = "SmsDev";

    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.smsdev.com.br/v1/";
    public int Type { get; set; } = 9;
    public int TimeoutSeconds { get; set; } = 30;
}

public sealed class ViaCepOptions
{
    public const string SectionName = "ViaCep";

    public string BaseUrl { get; set; } = "https://viacep.com.br/ws/";
    public int TimeoutSeconds { get; set; } = 15;
}

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "autonomousaudit";
    public string Audience { get; set; } = "autonomousaudit";
    public string SigningKey { get; set; } = string.Empty;
    public int LifetimeMinutes { get; set; } = 60;
}

public sealed class GoogleAuthOptions
{
    public const string SectionName = "Google";

    public string ClientId { get; set; } = string.Empty;
}

public sealed class FrontUrlOptions
{
    public const string SectionName = "Front";

    public string BaseUrl { get; set; } = "https://autonomousaudit.canada-software.com.br";
}

public sealed class AuthBootstrapOptions
{
    public const string SectionName = "Auth";

    public string Nome { get; set; } = "Administrador";
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public sealed class MailEncryptionOptions
{
    public const string SectionName = "Mail";

    public string EncryptionKey { get; set; } = string.Empty;
}

public sealed class NatsOptions
{
    public const string SectionName = "Nats";

    public string Url { get; set; } = "nats://nats:4222";
    public string ArquivoSubject { get; set; } = "ARQUIVOS";
    public string ArquivoStream { get; set; } = "ARQUIVOS";
    public string DropboxSubject { get; set; } = "dropbox";
    public string DropboxStream { get; set; } = "DROPBOX";
    public string DropboxConsumer { get; set; } = "dropbox-dispatch";
    public int DropboxMaxTentativas { get; set; } = 4;
    public int DropboxRetryDelaySeconds { get; set; } = 2;
    public string PerplexityPromptSubject { get; set; } = "PERPLEXITY_PROMPT";
    public string PerplexityPromptStream { get; set; } = "PERPLEXITY_PROMPT";
    public string PerplexityPromptConsumer { get; set; } = "perplexity-prompt";
    public string PerplexityArquivoSubject { get; set; } = "PERPLEXITY_ARQUIVO";
    public string PerplexityArquivoStream { get; set; } = "PERPLEXITY_ARQUIVO";
    public string PerplexityArquivoConsumer { get; set; } = "perplexity-arquivo";
    public int PerplexityMaxTentativas { get; set; } = 4;
    public int PerplexityRetryDelaySeconds { get; set; } = 2;
    public string MailSubject { get; set; } = "MAIL";
    public string MailStream { get; set; } = "MAIL";
    public string MailConsumer { get; set; } = "mail-dispatch";
    public int MailMaxTentativas { get; set; } = 4;
    public int MailRetryDelaySeconds { get; set; } = 2;
    public string CompraProcessarSubject { get; set; } = "COMPRA_PROCESSAR";
    public string CompraProcessarStream { get; set; } = "COMPRA_PROCESSAR";
    public string CompraProcessarConsumer { get; set; } = "compra-processar";
    public int CompraProcessarMaxTentativas { get; set; } = 4;
    public int CompraProcessarRetryDelaySeconds { get; set; } = 2;
    public int CompraProcessarTimeoutSeconds { get; set; } = 420;
    public int PublishTimeoutSeconds { get; set; } = 20;
    public string StagingPath { get; set; } = string.Empty;
}

internal static class IntegrationConfig
{
    public static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim() ?? string.Empty;

    public static void ApplyEnvironmentFallbacks(
        TitanSmtpOptions options,
        IConfiguration configuration)
    {
        options.Host = FirstNonEmpty(options.Host, configuration["WKF_SMTP_HOST"]);
        options.User = FirstNonEmpty(options.User, configuration["WKF_SMTP_USER"]);
        options.From = FirstNonEmpty(options.From, configuration["WKF_SMTP_FROM"], options.User);
        options.Password = FirstNonEmpty(options.Password, configuration["WKF_SMTP_PASSWORD"]);
        options.TlsMode = FirstNonEmpty(options.TlsMode, configuration["WKF_SMTP_TLS_MODE"], "ssl");

        if (int.TryParse(configuration["WKF_SMTP_PORT"], out var porta) && porta > 0)
        {
            options.Port = porta;
        }
        else if (options.Port <= 0)
        {
            options.Port = 465;
        }
    }

    public static void ApplyEnvironmentFallbacks(
        PerplexityOptions options,
        IConfiguration configuration)
    {
        options.ApiKey = FirstNonEmpty(options.ApiKey, configuration["PERPLEXITY_API_KEY"]);
        options.Model = FirstNonEmpty(options.Model, configuration["PERPLEXITY_MODEL"], "sonar");
        options.AgentModel = FirstNonEmpty(options.AgentModel, configuration["PERPLEXITY_AGENT_MODEL"], "openai/gpt-5-mini");
        if (string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            options.BaseUrl = "https://api.perplexity.ai/";
        }

        if (int.TryParse(configuration["PERPLEXITY_HTTP_TIMEOUT_SECONDS"], out var timeout) && timeout > 0)
        {
            options.TimeoutSeconds = timeout;
        }
        else if (options.TimeoutSeconds <= 0)
        {
            options.TimeoutSeconds = 300;
        }

        if (decimal.TryParse(configuration["PERPLEXITY_PRECO_INPUT_USD_M"], out var inputUsd) && inputUsd >= 0)
        {
            options.PrecoInputUsdPorMilhao = inputUsd;
        }

        if (decimal.TryParse(configuration["PERPLEXITY_PRECO_OUTPUT_USD_M"], out var outputUsd) && outputUsd >= 0)
        {
            options.PrecoOutputUsdPorMilhao = outputUsd;
        }

        if (decimal.TryParse(configuration["PERPLEXITY_TAXA_USD_BRL"], out var taxaBrl) && taxaBrl > 0)
        {
            options.TaxaUsdParaBrl = taxaBrl;
        }
    }

    public static void ApplyEnvironmentFallbacks(
        SmsDevOptions options,
        IConfiguration configuration)
    {
        options.ApiKey = FirstNonEmpty(options.ApiKey, configuration["SMSDEV_API_KEY"]);
        if (int.TryParse(configuration["SMSDEV_TYPE"], out var tipo) && tipo > 0)
        {
            options.Type = tipo;
        }
        else if (options.Type <= 0)
        {
            options.Type = 9;
        }

        if (string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            options.BaseUrl = "https://api.smsdev.com.br/v1/";
        }
    }

    public static void ApplyEnvironmentFallbacks(
        ViaCepOptions options,
        IConfiguration configuration)
    {
        options.BaseUrl = FirstNonEmpty(options.BaseUrl, configuration["VIACEP_BASE_URL"], "https://viacep.com.br/ws/");
        if (!options.BaseUrl.EndsWith('/'))
        {
            options.BaseUrl += "/";
        }

        if (int.TryParse(configuration["VIACEP_TIMEOUT_SECONDS"], out var timeout) && timeout > 0)
        {
            options.TimeoutSeconds = timeout;
        }
        else if (options.TimeoutSeconds <= 0)
        {
            options.TimeoutSeconds = 15;
        }
    }

    public static void ApplyEnvironmentFallbacks(
        JwtOptions options,
        IConfiguration configuration)
    {
        options.Issuer = FirstNonEmpty(options.Issuer, configuration["JWT_ISSUER"], "autonomousaudit");
        options.Audience = FirstNonEmpty(options.Audience, configuration["JWT_AUDIENCE"], "autonomousaudit");
        options.SigningKey = FirstNonEmpty(options.SigningKey, configuration["JWT_SIGNING_KEY"]);
        if (int.TryParse(configuration["JWT_LIFETIME_MINUTES"], out var minutos) && minutos > 0)
        {
            options.LifetimeMinutes = minutos;
        }
        else if (options.LifetimeMinutes <= 0)
        {
            options.LifetimeMinutes = 60;
        }
    }

    public static void ApplyEnvironmentFallbacks(
        GoogleAuthOptions options,
        IConfiguration configuration)
    {
        options.ClientId = FirstNonEmpty(
            options.ClientId,
            configuration["GOOGLE_CLIENT_ID"],
            "760062945320-btngfnmtu059p0nhomtt9v88npof4bc0.apps.googleusercontent.com");
    }

    public static void ApplyEnvironmentFallbacks(
        FrontUrlOptions options,
        IConfiguration configuration)
    {
        options.BaseUrl = FirstNonEmpty(
            options.BaseUrl,
            configuration["FRONT_BASE_URL"],
            "https://autonomousaudit.canada-software.com.br");
        options.BaseUrl = options.BaseUrl.TrimEnd('/');
    }

    public static void ApplyEnvironmentFallbacks(
        AuthBootstrapOptions options,
        IConfiguration configuration)
    {
        options.Nome = FirstNonEmpty(options.Nome, configuration["AUTH_BOOTSTRAP_NOME"], "Administrador");
        options.Email = FirstNonEmpty(options.Email, configuration["AUTH_BOOTSTRAP_EMAIL"]);
        options.Password = FirstNonEmpty(options.Password, configuration["AUTH_BOOTSTRAP_PASSWORD"]);
    }

    public static void ApplyEnvironmentFallbacks(
        MailEncryptionOptions options,
        IConfiguration configuration)
    {
        options.EncryptionKey = FirstNonEmpty(
            options.EncryptionKey,
            configuration["MAIL_ENCRYPTION_KEY"],
            configuration["Jwt:SigningKey"],
            configuration["JWT_SIGNING_KEY"]);
    }

    public static void ApplyEnvironmentFallbacks(
        NatsOptions options,
        IConfiguration configuration)
    {
        options.Url = FirstNonEmpty(options.Url, configuration["NATS_URL"], "nats://nats:4222");
        options.ArquivoSubject = FirstNonEmpty(options.ArquivoSubject, configuration["NATS_ARQUIVO_SUBJECT"], "ARQUIVOS");
        options.ArquivoStream = FirstNonEmpty(options.ArquivoStream, configuration["NATS_ARQUIVO_STREAM"], "ARQUIVOS");
        options.DropboxSubject = FirstNonEmpty(options.DropboxSubject, configuration["NATS_DROPBOX_SUBJECT"], "dropbox");
        options.DropboxStream = FirstNonEmpty(options.DropboxStream, configuration["NATS_DROPBOX_STREAM"], "DROPBOX");
        options.DropboxConsumer = FirstNonEmpty(options.DropboxConsumer, configuration["NATS_DROPBOX_CONSUMER"], "dropbox-dispatch");
        options.PerplexityPromptSubject = FirstNonEmpty(options.PerplexityPromptSubject, configuration["NATS_PERPLEXITY_PROMPT_SUBJECT"], "PERPLEXITY_PROMPT");
        options.PerplexityPromptStream = FirstNonEmpty(options.PerplexityPromptStream, configuration["NATS_PERPLEXITY_PROMPT_STREAM"], "PERPLEXITY_PROMPT");
        options.PerplexityPromptConsumer = FirstNonEmpty(options.PerplexityPromptConsumer, configuration["NATS_PERPLEXITY_PROMPT_CONSUMER"], "perplexity-prompt");
        options.PerplexityArquivoSubject = FirstNonEmpty(options.PerplexityArquivoSubject, configuration["NATS_PERPLEXITY_ARQUIVO_SUBJECT"], "PERPLEXITY_ARQUIVO");
        options.PerplexityArquivoStream = FirstNonEmpty(options.PerplexityArquivoStream, configuration["NATS_PERPLEXITY_ARQUIVO_STREAM"], "PERPLEXITY_ARQUIVO");
        options.PerplexityArquivoConsumer = FirstNonEmpty(options.PerplexityArquivoConsumer, configuration["NATS_PERPLEXITY_ARQUIVO_CONSUMER"], "perplexity-arquivo");
        options.MailSubject = FirstNonEmpty(options.MailSubject, configuration["NATS_MAIL_SUBJECT"], "MAIL");
        options.MailStream = FirstNonEmpty(options.MailStream, configuration["NATS_MAIL_STREAM"], "MAIL");
        options.MailConsumer = FirstNonEmpty(options.MailConsumer, configuration["NATS_MAIL_CONSUMER"], "mail-dispatch");
        options.StagingPath = FirstNonEmpty(options.StagingPath, configuration["NATS_STAGING_PATH"]);
        if (int.TryParse(configuration["NATS_DROPBOX_MAX_TENTATIVAS"], out var max) && max > 0)
        {
            options.DropboxMaxTentativas = max;
        }
        else if (options.DropboxMaxTentativas <= 0)
        {
            options.DropboxMaxTentativas = 4;
        }

        if (int.TryParse(configuration["NATS_DROPBOX_RETRY_DELAY_SECONDS"], out var delay) && delay > 0)
        {
            options.DropboxRetryDelaySeconds = delay;
        }
        else if (options.DropboxRetryDelaySeconds <= 0)
        {
            options.DropboxRetryDelaySeconds = 2;
        }

        if (int.TryParse(configuration["NATS_PERPLEXITY_MAX_TENTATIVAS"], out var pplxMax) && pplxMax > 0)
        {
            options.PerplexityMaxTentativas = pplxMax;
        }
        else if (options.PerplexityMaxTentativas <= 0)
        {
            options.PerplexityMaxTentativas = 4;
        }

        if (int.TryParse(configuration["NATS_PERPLEXITY_RETRY_DELAY_SECONDS"], out var pplxDelay) && pplxDelay > 0)
        {
            options.PerplexityRetryDelaySeconds = pplxDelay;
        }
        else if (options.PerplexityRetryDelaySeconds <= 0)
        {
            options.PerplexityRetryDelaySeconds = 2;
        }

        if (int.TryParse(configuration["NATS_MAIL_MAX_TENTATIVAS"], out var mailMax) && mailMax > 0)
        {
            options.MailMaxTentativas = mailMax;
        }
        else if (options.MailMaxTentativas <= 0)
        {
            options.MailMaxTentativas = 4;
        }

        if (int.TryParse(configuration["NATS_MAIL_RETRY_DELAY_SECONDS"], out var mailDelay) && mailDelay > 0)
        {
            options.MailRetryDelaySeconds = mailDelay;
        }
        else if (options.MailRetryDelaySeconds <= 0)
        {
            options.MailRetryDelaySeconds = 2;
        }
    }
}
