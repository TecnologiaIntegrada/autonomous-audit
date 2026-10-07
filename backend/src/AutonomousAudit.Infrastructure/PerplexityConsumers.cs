using System.Text.Json;
using AutonomousAudit.Application;
using AutonomousAudit.Application.Data;
using AutonomousAudit.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;

namespace AutonomousAudit.Infrastructure;

public sealed class PerplexityPromptConsumer : BackgroundService
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly INatsConnection _nats;
    private readonly IServiceScopeFactory _scopes;
    private readonly NatsOptions _options;
    private readonly ILogger<PerplexityPromptConsumer> _logger;

    public PerplexityPromptConsumer(
        INatsConnection nats,
        IServiceScopeFactory scopes,
        IOptions<NatsOptions> options,
        ILogger<PerplexityPromptConsumer> logger)
    {
        _nats = nats;
        _scopes = scopes;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConsumirAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Consumer PERPLEXITY_PROMPT interrompido. Nova tentativa em 5s");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task ConsumirAsync(CancellationToken stoppingToken)
    {
        var js = new NatsJSContext(_nats);
        await js.CreateOrUpdateStreamAsync(
            new StreamConfig(_options.PerplexityPromptStream, [_options.PerplexityPromptSubject]),
            stoppingToken);

        var consumer = await js.CreateOrUpdateConsumerAsync(
            _options.PerplexityPromptStream,
            new ConsumerConfig(_options.PerplexityPromptConsumer)
            {
                FilterSubject = _options.PerplexityPromptSubject,
                AckWait = TimeSpan.FromMinutes(10)
            },
            stoppingToken);

        _logger.LogInformation(
            "Consumer PERPLEXITY_PROMPT inscrito subject={Subject} stream={Stream} durable={Durable}",
            _options.PerplexityPromptSubject,
            _options.PerplexityPromptStream,
            _options.PerplexityPromptConsumer);

        await foreach (var msg in consumer.ConsumeAsync(
                           serializer: NatsRawSerializer<byte[]>.Default,
                           opts: new NatsJSConsumeOpts { MaxMsgs = 8 },
                           cancellationToken: stoppingToken))
        {
            try
            {
                await ProcessarAsync(msg.Data, stoppingToken);
                await msg.AckAsync(cancellationToken: stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha no processamento PERPLEXITY_PROMPT. NAK para reentrega");
                try
                {
                    await msg.NakAsync(cancellationToken: stoppingToken);
                }
                catch (Exception nak)
                {
                    _logger.LogWarning(nak, "Falha ao enviar NAK PERPLEXITY_PROMPT");
                }
            }
        }
    }

    private async Task ProcessarAsync(byte[]? payload, CancellationToken cancellationToken)
    {
        if (payload is null || payload.Length == 0)
        {
            return;
        }

        var evento = JsonSerializer.Deserialize<PerplexityPromptEvento>(payload, Json);
        if (evento is null || evento.Id == Guid.Empty)
        {
            _logger.LogWarning("Mensagem PERPLEXITY_PROMPT sem id");
            return;
        }

        using var scope = _scopes.CreateScope();
        var prompts = scope.ServiceProvider.GetRequiredService<IPerplexityPromptRepository>();
        var client = scope.ServiceProvider.GetRequiredService<IPerplexityClient>();
        var uso = scope.ServiceProvider.GetRequiredService<IRegistrarPerplexityUsoUsuario>();
        var operacao = await prompts.GetByIdAsync(evento.Id, cancellationToken)
            ?? throw new InvalidOperationException($"perplexity_prompt {evento.Id} ainda nao encontrado.");

        if (operacao.Status == PerplexityOperacaoStatus.Sucesso
            || (operacao.Status == PerplexityOperacaoStatus.Falha && !operacao.PodeTentar))
        {
            return;
        }

        var delay = Math.Max(1, _options.PerplexityRetryDelaySeconds);
        while (operacao.PodeTentar)
        {
            operacao.MarcarProcessando();
            await prompts.SaveChangesAsync(cancellationToken);
            try
            {
                byte[]? arquivo = null;
                if (!string.IsNullOrWhiteSpace(operacao.CaminhoLocal) && File.Exists(operacao.CaminhoLocal))
                {
                    arquivo = await File.ReadAllBytesAsync(operacao.CaminhoLocal, cancellationToken);
                }

                var resposta = await client.AnalisarAsync(
                    new PerplexityAgentePedido(operacao.Prompt, operacao.Modelo, arquivo, operacao.MimeType),
                    cancellationToken);
                operacao.MarcarSucesso(resposta.Modelo, resposta.Texto);
                await uso.RegistrarAsync(operacao.UsuarioId, resposta, cancellationToken);
                await prompts.SaveChangesAsync(cancellationToken);
                return;
            }
            catch (Exception ex)
            {
                operacao.RegistrarTentativaFalha(ex.Message);
                await prompts.SaveChangesAsync(cancellationToken);
                _logger.LogWarning(ex, "Falha PERPLEXITY_PROMPT id={Id} tentativas={Tentativas}", operacao.Id, operacao.Tentativas);
                if (operacao.PodeTentar)
                {
                    await Task.Delay(TimeSpan.FromSeconds(delay * Math.Pow(2, operacao.Tentativas - 1)), cancellationToken);
                }
            }
        }
    }
}

public sealed class PerplexityArquivoConsumer : BackgroundService
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly INatsConnection _nats;
    private readonly IServiceScopeFactory _scopes;
    private readonly NatsOptions _options;
    private readonly ILogger<PerplexityArquivoConsumer> _logger;

    public PerplexityArquivoConsumer(
        INatsConnection nats,
        IServiceScopeFactory scopes,
        IOptions<NatsOptions> options,
        ILogger<PerplexityArquivoConsumer> logger)
    {
        _nats = nats;
        _scopes = scopes;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConsumirAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Consumer PERPLEXITY_ARQUIVO interrompido. Nova tentativa em 5s");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task ConsumirAsync(CancellationToken stoppingToken)
    {
        var js = new NatsJSContext(_nats);
        await js.CreateOrUpdateStreamAsync(
            new StreamConfig(_options.PerplexityArquivoStream, [_options.PerplexityArquivoSubject]),
            stoppingToken);

        var consumer = await js.CreateOrUpdateConsumerAsync(
            _options.PerplexityArquivoStream,
            new ConsumerConfig(_options.PerplexityArquivoConsumer)
            {
                FilterSubject = _options.PerplexityArquivoSubject,
                AckWait = TimeSpan.FromMinutes(10)
            },
            stoppingToken);

        await foreach (var msg in consumer.ConsumeAsync(
                           serializer: NatsRawSerializer<byte[]>.Default,
                           opts: new NatsJSConsumeOpts { MaxMsgs = 4 },
                           cancellationToken: stoppingToken))
        {
            try
            {
                await ProcessarAsync(msg.Data, stoppingToken);
                await msg.AckAsync(cancellationToken: stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha no processamento PERPLEXITY_ARQUIVO. NAK para reentrega");
                try
                {
                    await msg.NakAsync(cancellationToken: stoppingToken);
                }
                catch (Exception nak)
                {
                    _logger.LogWarning(nak, "Falha ao enviar NAK PERPLEXITY_ARQUIVO");
                }
            }
        }
    }

    private async Task ProcessarAsync(byte[]? payload, CancellationToken cancellationToken)
    {
        if (payload is null || payload.Length == 0)
        {
            return;
        }

        var evento = JsonSerializer.Deserialize<PerplexityArquivoEvento>(payload, Json);
        if (evento is null || evento.Id == Guid.Empty)
        {
            return;
        }

        using var scope = _scopes.CreateScope();
        var arquivos = scope.ServiceProvider.GetRequiredService<IPerplexityArquivoRepository>();
        var nats = scope.ServiceProvider.GetRequiredService<IArquivoEventoPublisher>();
        var client = scope.ServiceProvider.GetRequiredService<IPerplexityClient>();
        var uso = scope.ServiceProvider.GetRequiredService<IRegistrarPerplexityUsoUsuario>();
        var operacao = await arquivos.GetByIdAsync(evento.Id, cancellationToken)
            ?? throw new InvalidOperationException($"perplexity_arquivo {evento.Id} ainda nao encontrado.");

        if (!operacao.DropboxInscrito)
        {
            await nats.PublicarDropboxAsync(
                new DropboxDispatchEvento(
                    operacao.DropboxDispatchId,
                    operacao.ArquivoRecebidoId,
                    operacao.CaminhoDropbox,
                    operacao.NomeArquivo,
                    operacao.NomeOriginal,
                    operacao.CaminhoLocal),
                cancellationToken);
            operacao.MarcarDropboxInscrito();
            await arquivos.SaveChangesAsync(cancellationToken);
        }

        if (operacao.Status == PerplexityOperacaoStatus.Sucesso
            || (operacao.Status == PerplexityOperacaoStatus.Falha && !operacao.PodeTentar))
        {
            return;
        }

        var delay = Math.Max(1, _options.PerplexityRetryDelaySeconds);
        while (operacao.PodeTentar)
        {
            operacao.MarcarProcessando();
            await arquivos.SaveChangesAsync(cancellationToken);
            try
            {
                if (!File.Exists(operacao.CaminhoLocal))
                {
                    throw new FileNotFoundException("Binario local do Perplexity arquivo nao encontrado.", operacao.CaminhoLocal);
                }

                var bytes = await File.ReadAllBytesAsync(operacao.CaminhoLocal, cancellationToken);
                var resposta = await client.AnalisarAsync(
                    new PerplexityAgentePedido(operacao.Prompt, operacao.Modelo, bytes, operacao.MimeType),
                    cancellationToken);
                operacao.MarcarSucesso(resposta.Modelo, resposta.Texto);
                await uso.RegistrarAsync(operacao.UsuarioId, resposta, cancellationToken);
                await arquivos.SaveChangesAsync(cancellationToken);
                return;
            }
            catch (Exception ex)
            {
                operacao.RegistrarTentativaFalha(ex.Message);
                await arquivos.SaveChangesAsync(cancellationToken);
                if (operacao.PodeTentar)
                {
                    await Task.Delay(TimeSpan.FromSeconds(delay * Math.Pow(2, operacao.Tentativas - 1)), cancellationToken);
                }
            }
        }
    }
}
