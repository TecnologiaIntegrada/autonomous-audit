using System.Text.Json;
using AutonomousAudit.Application;
using AutonomousAudit.Application.Data;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;

namespace AutonomousAudit.Infrastructure;

public sealed class NatsArquivoEventoPublisher : IArquivoEventoPublisher
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly INatsConnection _nats;
    private readonly NatsOptions _options;
    private readonly ILogger<NatsArquivoEventoPublisher> _logger;
    private readonly SemaphoreSlim _streamGate = new(1, 1);
    private readonly HashSet<string> _streamsProntos = new(StringComparer.Ordinal);

    public NatsArquivoEventoPublisher(
        INatsConnection nats,
        IOptions<NatsOptions> options,
        ILogger<NatsArquivoEventoPublisher> logger)
    {
        _nats = nats;
        _options = options.Value;
        _logger = logger;
    }

    public Task<ArquivoEventoPublicado> PublicarRecebidoAsync(
        ArquivoRecebidoEvento evento,
        CancellationToken cancellationToken = default) =>
        PublicarAsync(
            _options.ArquivoStream,
            _options.ArquivoSubject,
            evento,
            evento.Id,
            cancellationToken);

    public Task<ArquivoEventoPublicado> PublicarDropboxAsync(
        DropboxDispatchEvento evento,
        CancellationToken cancellationToken = default) =>
        PublicarAsync(
            _options.DropboxStream,
            _options.DropboxSubject,
            evento,
            evento.TransacaoId,
            cancellationToken);

    public Task<ArquivoEventoPublicado> PublicarPerplexityPromptAsync(
        PerplexityPromptEvento evento,
        CancellationToken cancellationToken = default) =>
        PublicarAsync(
            _options.PerplexityPromptStream,
            _options.PerplexityPromptSubject,
            evento,
            evento.Id,
            cancellationToken);

    public Task<ArquivoEventoPublicado> PublicarPerplexityArquivoAsync(
        PerplexityArquivoEvento evento,
        CancellationToken cancellationToken = default) =>
        PublicarAsync(
            _options.PerplexityArquivoStream,
            _options.PerplexityArquivoSubject,
            evento,
            evento.Id,
            cancellationToken);

    public Task<ArquivoEventoPublicado> PublicarMailAsync(
        MailEnvioEvento evento,
        CancellationToken cancellationToken = default) =>
        PublicarAsync(
            _options.MailStream,
            _options.MailSubject,
            evento,
            evento.Id,
            cancellationToken);

    public Task<ArquivoEventoPublicado> PublicarCompraProcessarAsync(
        CompraProcessarEvento evento,
        CancellationToken cancellationToken = default) =>
        PublicarAsync(
            _options.CompraProcessarStream,
            _options.CompraProcessarSubject,
            evento,
            evento.MensagemId,
            cancellationToken);

    private async Task<ArquivoEventoPublicado> PublicarAsync<T>(
        string stream,
        string subject,
        T evento,
        Guid msgId,
        CancellationToken cancellationToken)
    {
        var timeoutSeconds = _options.PublishTimeoutSeconds > 0 ? _options.PublishTimeoutSeconds : 20;
        try
        {
            return await PublicarNucleoAsync(stream, subject, evento, msgId, cancellationToken)
                .WaitAsync(TimeSpan.FromSeconds(timeoutSeconds), cancellationToken);
        }
        catch (TimeoutException)
        {
            _logger.LogError(
                "Timeout ao publicar no NATS subject={Subject} stream={Stream} msg={MsgId} timeoutSeconds={TimeoutSeconds}",
                subject,
                stream,
                msgId,
                timeoutSeconds);
            throw new TimeoutException($"Timeout de {timeoutSeconds}s ao publicar no NATS {subject}.");
        }
    }

    private async Task<ArquivoEventoPublicado> PublicarNucleoAsync<T>(
        string stream,
        string subject,
        T evento,
        Guid msgId,
        CancellationToken cancellationToken)
    {
        var js = new NatsJSContext(_nats);
        await GarantirStreamAsync(js, stream, subject, cancellationToken);

        var payload = JsonSerializer.SerializeToUtf8Bytes(evento, Json);
        try
        {
            var ack = await js.PublishAsync(
                subject,
                payload,
                serializer: NatsRawSerializer<byte[]>.Default,
                opts: new NatsJSPubOpts { MsgId = msgId.ToString("N") },
                cancellationToken: cancellationToken);

            ack.EnsureSuccess();

            _logger.LogInformation(
                "Evento publicado no NATS subject={Subject} stream={Stream} seq={Seq} msg={MsgId}",
                subject,
                ack.Stream ?? stream,
                ack.Seq,
                msgId);

            return new ArquivoEventoPublicado(subject, ack.Stream ?? stream, ack.Seq);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Falha ao publicar no NATS subject={Subject} stream={Stream} msg={MsgId} tipo={Tipo} cadeia={Cadeia}",
                subject,
                stream,
                msgId,
                ex.GetType().Name,
                LogExcecao.Cadeia(ex));
            throw;
        }
    }

    private async Task GarantirStreamAsync(
        INatsJSContext js,
        string stream,
        string subject,
        CancellationToken cancellationToken)
    {
        if (_streamsProntos.Contains(stream))
        {
            return;
        }

        await _streamGate.WaitAsync(cancellationToken);
        try
        {
            if (_streamsProntos.Contains(stream))
            {
                return;
            }

            await js.CreateOrUpdateStreamAsync(new StreamConfig(stream, [subject]), cancellationToken);
            _streamsProntos.Add(stream);
            _logger.LogInformation("Stream JetStream {Stream} pronto para o subject {Subject}", stream, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Falha ao garantir stream JetStream {Stream} subject={Subject} tipo={Tipo} cadeia={Cadeia}",
                stream,
                subject,
                ex.GetType().Name,
                LogExcecao.Cadeia(ex));
            throw;
        }
        finally
        {
            _streamGate.Release();
        }
    }
}
