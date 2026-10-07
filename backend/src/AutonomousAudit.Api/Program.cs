using AutonomousAudit.Api;
using AutonomousAudit.Application;
using AutonomousAudit.Infrastructure;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Serilog;
using System.Threading.RateLimiting;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Warning()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.UseSeqLogging();

    var maxUploadBytes = builder.Configuration.GetValue("Arquivos:MaxRequestBodyBytes", 104_857_600L);
    if (maxUploadBytes < 1)
    {
        maxUploadBytes = 104_857_600L;
    }

    builder.WebHost.ConfigureKestrel(options =>
    {
        options.Limits.MaxRequestBodySize = maxUploadBytes;
    });
    builder.Services.Configure<KestrelServerOptions>(options =>
    {
        options.Limits.MaxRequestBodySize = maxUploadBytes;
    });
    builder.Services.Configure<IISServerOptions>(options =>
    {
        options.MaxRequestBodySize = maxUploadBytes;
    });
    builder.Services.Configure<FormOptions>(options =>
    {
        options.MultipartBodyLengthLimit = maxUploadBytes;
        options.ValueLengthLimit = int.MaxValue;
        options.MultipartHeadersLengthLimit = 16_384;
    });

    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new()
        {
            Title = "Autonomous Audit API",
            Version = "v1",
            Description =
                "API do Autonomous Audit em **autonomousaudit.canada-software.com.br**.\n\n" +
                "**Como acessar.** Quase todas as rotas exigem `Authorization: Bearer {JWT}`. " +
                "O token sai de `POST /v1/auth/login` e vale **1 hora**, sem refresh: ao expirar, autentique de novo. " +
                "Além do JWT, cada rota exige um **recurso** no perfil do usuário (ex.: `email.send`). Sem o recurso, a resposta é 403. " +
                "Nas rotas `/v1/usuarios`, o recurso so autoriza o **proprio** cadastro; para listar, criar ou alterar outros usuarios o ID precisa estar em `adm_sys`.\n\n" +
                "**Síncrono vs. fila.** Endpoints de ingestão de arquivo, e-mail e Qwen assíncrono **não esperam** o provedor externo. " +
                "Eles gravam um registro, publicam no NATS e devolvem o id. O status é consultado depois no GET correspondente.\n\n" +
                "**Códigos HTTP usuais.** 200/201/202 sucesso; 400 dados inválidos; 401 token ausente ou expirado; " +
                "403 permissão insuficiente; 404 não encontrado; 502 falha em Postgres, NATS, SMTP, Qwen, Dropbox ou SMSDev."
        });
        options.DocumentFilter<SwaggerTagDescriptionsFilter>();
        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "JWT HS256 com validade de **1 hora**. Informe exatamente: `Bearer {token}` (espaço após Bearer). Sem refresh: ao expirar, chame de novo o POST /v1/auth/login."
        });
        options.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                },
                Array.Empty<string>()
            }
        });
    });
    builder.Services.AddHealthChecks();
    builder.Services.AddRequestTimeouts();
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.OnRejected = async (contexto, token) =>
        {
            SeqErrorLog.Capturar(
                contexto.HttpContext,
                "limite",
                "Limite de consultas de endereco desta API foi excedido.");
            await Results.Problem(
                    title: "Limite excedido",
                    detail: "Limite de consultas de endereco desta API foi excedido. Tente novamente em instantes.",
                    statusCode: StatusCodes.Status429TooManyRequests)
                .ExecuteAsync(contexto.HttpContext);
        };
        options.AddPolicy("viacep", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                httpContext.Connection.RemoteIpAddress?.ToString() ?? "anon",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 30,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                    AutoReplenishment = true
                }));
    });
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("front", policy =>
        {
            policy.WithOrigins(
                    "https://autonomousaudit.canada-software.com.br",
                    "http://localhost:3000")
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
    });
    builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);

    var app = builder.Build();
    app.Logger.LogInformation(
        "Limite de upload {MaxBytes} bytes ({MaxMb:0} MB)",
        maxUploadBytes,
        maxUploadBytes / (1024d * 1024d));
    app.UseSeqRequestLogging();
    app.UseMiddleware<HttpErrorLoggingMiddleware>();
    app.UseMiddleware<ErrorHandlerMiddleware>();
    app.UseRequestTimeouts();
    app.UseRateLimiter();
    app.UseCors("front");

    app.UseSwagger();
    app.UseSwaggerUI();

    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
        try
        {
            await db.Database.MigrateAsync();
            Log.Information("Migracoes do Postgres aplicadas");
            await scope.ServiceProvider.GetRequiredService<AcessoBootstrap>().ExecutarAsync();
            Log.Information("Catalogo de recursos e usuario inicial sincronizados");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Falha ao aplicar migracoes no Postgres");
            if (!app.Environment.IsDevelopment())
            {
                throw;
            }

            Log.Warning("API iniciando sem Postgres no ambiente Development");
        }
    }

    var seqUrl = builder.Configuration["Seq:ServerUrl"] ?? builder.Configuration["SEQ_SERVER_URL"];
    if (!string.IsNullOrWhiteSpace(seqUrl))
    {
        Log.Information("Seq habilitado em {SeqUrl}", seqUrl.TrimEnd('/'));
    }
    else
    {
        Log.Warning("Seq nao configurado. Informe Seq:ServerUrl ou SEQ_SERVER_URL");
    }

    app.MapHealthChecks("/healthz")
        .WithTags("Saude")
        .WithSummary("Probe de liveness da API")
        .WithDescription(SwaggerDocs.Bloco(
            "Indica se o processo da API está no ar. Usado pelo Kubernetes para saber se o pod deve continuar recebendo tráfego de liveness.",
            "Monitoramento e orquestração — não é uma rota de negócio.",
            "Público. Sem JWT e sem recurso de permissão.",
            "Responde se o host está saudável segundo os health checks registrados (hoje um check básico de processo).",
            "- **200** aplicação viva."));
    app.MapHealthChecks("/health")
        .WithTags("Saude")
        .WithSummary("Probe de saúde da API")
        .WithDescription(SwaggerDocs.Bloco(
            "Mesmo papel prático de /healthz: confirmar que a API responde. Mantido para clientes e dashboards que já usam /health.",
            "Monitoramento externo, load balancer ou script de verificação.",
            "Público. Sem JWT.",
            "Health check ASP.NET padrão.",
            "- **200** aplicação saudável."));
    app.MapAuth();
    app.MapUsuarios();
    app.MapAdministradores();
    app.MapCatalogo();
    app.MapArquivos();
    app.MapDropbox();
    app.MapEmails();
    app.MapPerplexity();
    app.MapSms();
    app.MapEnderecos();
    app.MapCompras();

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "API encerrada na inicializacao");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}
