using Microsoft.AspNetCore.Http.Features;
using Serilog;
using Serilog.Enrichers.Sensitive;
using Serilog.Events;

namespace AutonomousAudit.Api;

public static class SeqLogging
{
    private static readonly string[] PropriedadesSensiveis =
    [
        "senha", "Senha", "password", "Password",
        "token", "Token", "senha_hash", "SenhaHash",
        "Authorization", "authorization",
        "sms_auth_code", "SmsAuthCode", "email_auth_code", "EmailAuthCode",
        "EncryptionKey", "SigningKey", "ApiKey"
    ];

    public static void UseSeqLogging(this WebApplicationBuilder builder)
    {
        builder.Host.UseSerilog((ctx, _, config) =>
        {
            var seqUrl = FirstNonEmpty(ctx.Configuration["Seq:ServerUrl"], ctx.Configuration["SEQ_SERVER_URL"]);
            var seqKey = FirstNonEmpty(ctx.Configuration["Seq:ApiKey"], ctx.Configuration["SEQ_API_KEY"]);

            config
                .MinimumLevel.Warning()
                .MinimumLevel.Override("AutonomousAudit", LogEventLevel.Information)
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.AspNetCore.Hosting.Diagnostics", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
                .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
                .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .Enrich.With<SeqExceptionEnricher>()
                .Enrich.WithProperty("Application", "AutonomousAudit")
                .Enrich.WithProperty("Environment", ctx.HostingEnvironment.EnvironmentName)
                .Enrich.WithSensitiveDataMasking(options =>
                {
                    options.MaskValue = "***SECRET***";
                    foreach (var propriedade in PropriedadesSensiveis)
                    {
                        options.MaskProperties.Add(propriedade);
                    }
                })
                .WriteTo.Console();

            if (!string.IsNullOrWhiteSpace(seqUrl))
            {
                config.WriteTo.Seq(
                    seqUrl.TrimEnd('/'),
                    apiKey: string.IsNullOrWhiteSpace(seqKey) ? null : seqKey,
                    restrictedToMinimumLevel: LogEventLevel.Information);
            }
        });
    }

    public static void UseSeqRequestLogging(this WebApplication app)
    {
        app.UseSerilogRequestLogging(options =>
        {
            options.GetLevel = (context, elapsed, exception) =>
            {
                var path = context.Request.Path;
                if (path.StartsWithSegments("/health") || path.StartsWithSegments("/healthz"))
                {
                    return LogEventLevel.Verbose;
                }

                if (exception is not null || context.Response.StatusCode >= 500)
                {
                    return LogEventLevel.Error;
                }

                if (context.Response.StatusCode >= 400)
                {
                    return LogEventLevel.Warning;
                }

                if (path.StartsWithSegments("/v1/arquivos") || path.StartsWithSegments("/v1/sms") || path.StartsWithSegments("/v1/enderecos"))
                {
                    return LogEventLevel.Information;
                }

                return LogEventLevel.Verbose;
            };

            options.MessageTemplate =
                "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";

            options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
            {
                diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
                diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
                diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.ToString());
                diagnosticContext.Set("TraceId", httpContext.TraceIdentifier);
                diagnosticContext.Set("ContentType", httpContext.Request.ContentType ?? string.Empty);
                diagnosticContext.Set("ContentLength", httpContext.Request.ContentLength ?? 0);
                diagnosticContext.Set("HasBearer", httpContext.Request.Headers.Authorization.Count > 0);
                var limite = httpContext.Features.Get<IHttpMaxRequestBodySizeFeature>()?.MaxRequestBodySize;
                if (limite is not null)
                {
                    diagnosticContext.Set("MaxRequestBodySize", limite.Value);
                }
                if (httpContext.Items[RecursoAuthorization.HttpItemUsuarioId] is Guid usuarioId)
                {
                    diagnosticContext.Set("UsuarioId", usuarioId);
                }

                if (httpContext.Items[RecursoAuthorization.HttpItemUsuarioEmail] is string email)
                {
                    diagnosticContext.Set("UsuarioEmail", email);
                }
            };
        });

        app.Use(async (http, next) =>
        {
            await next();
            if (http.Response.StatusCode == StatusCodes.Status413PayloadTooLarge)
            {
                var limite = http.Features.Get<IHttpMaxRequestBodySizeFeature>()?.MaxRequestBodySize;
                SeqErrorLog.Capturar(
                    http,
                    "payload-grande",
                    $"Corpo recusado. contentLength={http.Request.ContentLength} limite={limite}");
                Log.Warning(
                    "HTTP 413 corpo recusado path={Path} contentLength={ContentLength} limiteKestrel={Limite} trace={TraceId}",
                    http.Request.Path.Value,
                    http.Request.ContentLength,
                    limite,
                    http.TraceIdentifier);
            }
        });
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
}
