using AutonomousAudit.Application.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NATS.Client.Core;

namespace AutonomousAudit.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:Default nao configurada. Use user-secrets, appsettings.Development.json, ConnectionStrings__Default ou o Secret api-secret.");
        }

        services.AddDbContext<AuditDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddSingleton<ISqlConnectionFactory, NpgsqlSqlConnectionFactory>();
        services.AddScoped<IUsuarioConsulta, UsuarioConsulta>();
        services.AddScoped<ICatalogoConsulta, CatalogoConsulta>();
        services.AddScoped<IDropboxDispatchConsulta, DropboxDispatchConsulta>();
        services.AddScoped<IContaEmailConsulta, ContaEmailConsulta>();
        services.AddScoped<IArquivoRecebidoRepository, ArquivoRecebidoRepository>();
        services.AddScoped<IDropboxDispatchRepository, DropboxDispatchRepository>();
        services.AddScoped<IPerplexityPromptRepository, PerplexityPromptRepository>();
        services.AddScoped<IPerplexityArquivoRepository, PerplexityArquivoRepository>();
        services.AddSingleton<IArquivoStaging, ArquivoStagingDisco>();
        services.AddScoped<IContaEmailRepository, ContaEmailRepository>();
        services.AddScoped<IMailLogRepository, MailLogRepository>();
        services.AddScoped<IUsuarioRepository, UsuarioRepository>();
        services.AddScoped<IUsuarioPerplexityUsoMensalRepository, UsuarioPerplexityUsoMensalRepository>();
        services.AddScoped<IRegistrarPerplexityUsoUsuario, RegistrarPerplexityUsoUsuario>();
        services.AddSingleton<IPerplexityDocumentoPreparador, PerplexityDocumentoPreparador>();
        services.AddScoped<IAdministradorSistemaRepository, AdministradorSistemaRepository>();
        services.AddScoped<ICompraRepository, CompraRepository>();
        services.AddScoped<IFornecedorRepository, FornecedorRepository>();
        services.AddScoped<IProdutoRepository, ProdutoRepository>();
        services.AddScoped<IServicoRepository, ServicoRepository>();
        services.AddSingleton<IReciboPdfMontador, ReciboPdfMontador>();
        services.AddSingleton<ICompraDropboxPaths, CompraDropboxPaths>();
        services.AddScoped<ICatalogoPermissaoRepository, CatalogoPermissaoRepository>();
        services.AddSingleton<ISenhaHasher, SenhaHasher>();
        services.AddOptions<MailEncryptionOptions>()
            .Bind(configuration.GetSection(MailEncryptionOptions.SectionName))
            .PostConfigure(options => IntegrationConfig.ApplyEnvironmentFallbacks(options, configuration))
            .Validate(options => (options.EncryptionKey?.Trim().Length ?? 0) >= 32,
                "Mail:EncryptionKey nao configurada ou com menos de 32 caracteres. Informe Mail:EncryptionKey, MAIL_ENCRYPTION_KEY ou Jwt:SigningKey.")
            .ValidateOnStart();
        services.AddSingleton<ISegredoProtector, AesSegredoProtector>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddSingleton<IGoogleIdTokenValidator, GoogleIdTokenValidator>();
        services.AddSingleton<IFrontUrl, FrontUrlSettings>();
        services.AddScoped<IVerificadorAcesso, VerificadorAcesso>();
        services.AddScoped<AcessoBootstrap>();

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .PostConfigure(options => IntegrationConfig.ApplyEnvironmentFallbacks(options, configuration));
        services.AddOptions<GoogleAuthOptions>()
            .Bind(configuration.GetSection(GoogleAuthOptions.SectionName))
            .PostConfigure(options => IntegrationConfig.ApplyEnvironmentFallbacks(options, configuration));
        services.AddOptions<FrontUrlOptions>()
            .Bind(configuration.GetSection(FrontUrlOptions.SectionName))
            .PostConfigure(options => IntegrationConfig.ApplyEnvironmentFallbacks(options, configuration));
        services.AddOptions<AuthBootstrapOptions>()
            .Bind(configuration.GetSection(AuthBootstrapOptions.SectionName))
            .PostConfigure(options => IntegrationConfig.ApplyEnvironmentFallbacks(options, configuration));
        services.AddOptions<NatsOptions>()
            .Bind(configuration.GetSection(NatsOptions.SectionName))
            .PostConfigure(options => IntegrationConfig.ApplyEnvironmentFallbacks(options, configuration));
        services.AddSingleton<INatsConnection>(sp =>
        {
            var nats = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<NatsOptions>>().Value;
            return new NatsConnection(new NatsOpts { Url = nats.Url });
        });
        services.AddSingleton<IArquivoEventoPublisher, NatsArquivoEventoPublisher>();
        services.AddHostedService<DropboxDispatchConsumer>();
        services.AddHostedService<PerplexityPromptConsumer>();
        services.AddHostedService<PerplexityArquivoConsumer>();
        services.AddHostedService<MailDispatchConsumer>();
        services.AddHostedService<CompraProcessarConsumer>();
        services.AddSingleton<AuthDesafioWorker>();
        services.AddSingleton<IAuthDesafioPublisher>(sp => sp.GetRequiredService<AuthDesafioWorker>());
        services.AddHostedService(sp => sp.GetRequiredService<AuthDesafioWorker>());
        services.Configure<DropboxOptions>(configuration.GetSection(DropboxOptions.SectionName));
        services.AddSingleton<IDropboxDocumentos, DropboxDocumentos>();

        services.AddOptions<TitanSmtpOptions>()
            .Bind(configuration.GetSection(TitanSmtpOptions.SectionName))
            .PostConfigure(options => IntegrationConfig.ApplyEnvironmentFallbacks(options, configuration));
        services.AddSingleton<ITitanSmtpSettings, TitanSmtpSettings>();
        services.AddSingleton<IEmailSender, MailKitEmailSender>();

        services.AddOptions<PerplexityOptions>()
            .Bind(configuration.GetSection(PerplexityOptions.SectionName))
            .PostConfigure(options => IntegrationConfig.ApplyEnvironmentFallbacks(options, configuration));
        services.AddHttpClient<IPerplexityClient, PerplexityClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<PerplexityOptions>>().Value;
            var baseUrl = string.IsNullOrWhiteSpace(options.BaseUrl)
                ? "https://api.perplexity.ai/"
                : options.BaseUrl.TrimEnd('/') + "/";
            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = TimeSpan.FromSeconds(Math.Max(30, Math.Max(options.TimeoutSeconds, 300)));
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        });

        services.AddOptions<SmsDevOptions>()
            .Bind(configuration.GetSection(SmsDevOptions.SectionName))
            .PostConfigure(options => IntegrationConfig.ApplyEnvironmentFallbacks(options, configuration));
        services.AddHttpClient<ISmsDevClient, SmsDevClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<SmsDevOptions>>().Value;
            var baseUrl = string.IsNullOrWhiteSpace(options.BaseUrl)
                ? "https://api.smsdev.com.br/v1/"
                : options.BaseUrl.TrimEnd('/') + "/";
            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = TimeSpan.FromSeconds(Math.Max(15, options.TimeoutSeconds));
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        });

        services.AddOptions<ViaCepOptions>()
            .Bind(configuration.GetSection(ViaCepOptions.SectionName))
            .PostConfigure(options => IntegrationConfig.ApplyEnvironmentFallbacks(options, configuration))
            .Validate(options =>
                    Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var uri)
                    && uri.Scheme == Uri.UriSchemeHttps,
                "ViaCep:BaseUrl deve ser uma URL HTTPS (padrao https://viacep.com.br/ws/).")
            .ValidateOnStart();
        services.AddHttpClient<IViaCepClient, ViaCepClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ViaCepOptions>>().Value;
            var baseUrl = string.IsNullOrWhiteSpace(options.BaseUrl)
                ? "https://viacep.com.br/ws/"
                : options.BaseUrl.TrimEnd('/') + "/";
            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = TimeSpan.FromSeconds(Math.Max(5, options.TimeoutSeconds));
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        });

        return services;
    }
}
