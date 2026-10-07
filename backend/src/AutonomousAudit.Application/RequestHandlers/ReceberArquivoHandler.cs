using AutonomousAudit.Application;
using AutonomousAudit.Application.Data;
using AutonomousAudit.Domain;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AutonomousAudit.Application.RequestHandlers;

public record ReceberArquivoCommand(
    string Usuario,
    string NomeArquivo,
    long TamanhoBytes,
    Stream Conteudo,
    Guid UsuarioId)
    : ICommand<ReceberArquivoResult>;

public abstract record ReceberArquivoResult
{
    public static ReceberArquivoResult Created(
        ArquivoRecebido arquivo,
        DropboxDispatch dispatch,
        ArquivoEventoPublicado natsArquivos,
        ArquivoEventoPublicado? natsDropbox) =>
        new ReceberArquivoCreated(arquivo, dispatch, natsArquivos, natsDropbox);

    public static ReceberArquivoResult BadRequest(string message) => new ReceberArquivoBadRequest(message);
    public static ReceberArquivoResult Fail(string message) => new ReceberArquivoFail(message);
}

public record ReceberArquivoCreated(
    ArquivoRecebido Arquivo,
    DropboxDispatch Dispatch,
    ArquivoEventoPublicado NatsArquivos,
    ArquivoEventoPublicado? NatsDropbox) : ReceberArquivoResult;

public record ReceberArquivoBadRequest(string Message) : ReceberArquivoResult;
public record ReceberArquivoFail(string Message) : ReceberArquivoResult;

public sealed class ReceberArquivoCommandValidator : AbstractValidator<ReceberArquivoCommand>
{
    public ReceberArquivoCommandValidator()
    {
        RuleFor(x => x.Usuario)
            .NotEmpty().WithMessage("O usuario (e-mail) remetente e obrigatorio.")
            .EmailAddress().WithMessage("Informe um e-mail valido.");

        RuleFor(x => x.NomeArquivo)
            .NotEmpty().WithMessage("O arquivo e obrigatorio.");

        RuleFor(x => x.TamanhoBytes)
            .GreaterThan(0).WithMessage("O arquivo nao pode estar vazio.");

        RuleFor(x => x.Conteudo)
            .NotNull().WithMessage("O conteudo do arquivo e obrigatorio.");

        RuleFor(x => x.UsuarioId).NotEmpty();
    }
}

public sealed class ReceberArquivoHandler : IRequestHandler<ReceberArquivoCommand, ReceberArquivoResult>
{
    private const int MaxTentativasDropboxNats = 3;

    private readonly IArquivoRecebidoRepository _arquivos;
    private readonly IDropboxDispatchRepository _dispatches;
    private readonly IArquivoEventoPublisher _nats;
    private readonly IArquivoStaging _staging;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<ReceberArquivoHandler> _logger;

    public ReceberArquivoHandler(
        IArquivoRecebidoRepository arquivos,
        IDropboxDispatchRepository dispatches,
        IArquivoEventoPublisher nats,
        IArquivoStaging staging,
        IUnitOfWork uow,
        ILogger<ReceberArquivoHandler> logger)
    {
        _arquivos = arquivos;
        _dispatches = dispatches;
        _nats = nats;
        _staging = staging;
        _uow = uow;
        _logger = logger;
    }

    public async Task<ReceberArquivoResult> Handle(ReceberArquivoCommand request, CancellationToken cancellationToken)
    {
        var etapa = "inicio";
        string? caminhoLocal = null;
        ArquivoRecebido? arquivo = null;
        DropboxDispatch? dispatch = null;
        var persistido = false;
        ArquivoEventoPublicado? natsArquivos = null;

        try
        {
            _logger.LogInformation(
                "Recebendo arquivo nome={NomeArquivo} usuario={Usuario} bytes={TamanhoBytes} contentTypeStream={CanRead}",
                request.NomeArquivo,
                request.Usuario,
                request.TamanhoBytes,
                request.Conteudo.CanRead);

            var tamanhoMb = Math.Round(request.TamanhoBytes / (1024m * 1024m), 6, MidpointRounding.AwayFromZero);
            var nomeOriginal = DropboxPathHelper.NomeOriginal(request.NomeArquivo);
            arquivo = new ArquivoRecebido(
                nomeOriginal,
                tamanhoMb,
                DateTimeOffset.UtcNow,
                request.Usuario.Trim());

            var nomeDropbox = DropboxPathHelper.NomeDropboxPorId(arquivo.Id, nomeOriginal);
            var destino = DropboxPathHelper.Destino(null, nomeDropbox, request.UsuarioId);

            etapa = "staging";
            caminhoLocal = await _staging.SalvarAsync(arquivo.Id, request.Conteudo, cancellationToken);
            var bytesLocais = new FileInfo(caminhoLocal).Length;
            _logger.LogInformation(
                "Staging gravado arquivoId={ArquivoId} caminho={Caminho} bytes={Bytes}",
                arquivo.Id,
                caminhoLocal,
                bytesLocais);
            if (bytesLocais == 0)
            {
                _staging.Excluir(caminhoLocal);
                _logger.LogWarning(
                    "Ingest rejeitado arquivo vazio no disco nome={NomeArquivo} usuario={Usuario} caminho={Caminho}",
                    request.NomeArquivo,
                    request.Usuario,
                    caminhoLocal);
                return ReceberArquivoResult.BadRequest("O arquivo nao pode estar vazio.");
            }

            dispatch = new DropboxDispatch(
                arquivo.Id,
                arquivo.Id,
                nomeDropbox,
                nomeOriginal,
                caminhoLocal,
                destino);

            etapa = "postgres";
            await _arquivos.AddAsync(arquivo, cancellationToken);
            await _dispatches.AddAsync(dispatch, cancellationToken);
            await _arquivos.SaveChangesAsync(cancellationToken);
            await _uow.CommitAsync(cancellationToken);
            persistido = true;
            _logger.LogInformation(
                "Ingest persistido transacao={TransacaoId} arquivoId={ArquivoId} destino={Destino} nomeOriginal={NomeOriginal} nomeDropbox={NomeArquivo}",
                dispatch.Id,
                arquivo.Id,
                destino,
                dispatch.NomeOriginal,
                dispatch.NomeArquivo);

            etapa = "nats-arquivos";
            natsArquivos = await _nats.PublicarRecebidoAsync(
                new ArquivoRecebidoEvento(
                    arquivo.Id,
                    arquivo.NomeArquivo,
                    request.TamanhoBytes,
                    arquivo.TamanhoMb,
                    arquivo.DataHora,
                    arquivo.UsuarioRemetente,
                    destino),
                cancellationToken);

            etapa = "nats-dropbox";
            var natsDropbox = await PublicarDropboxComRetryAsync(
                dispatch,
                destino,
                caminhoLocal,
                cancellationToken);

            if (natsDropbox is null)
            {
                _logger.LogWarning(
                    "Ingest aceito sem fila dropbox transacao={TransacaoId} natsArquivos={ArquivosSeq} destino={Destino} arquivoMantido={Caminho}",
                    dispatch.Id,
                    natsArquivos.Sequencia,
                    destino,
                    caminhoLocal);
                return ReceberArquivoResult.Created(arquivo, dispatch, natsArquivos, null);
            }

            _logger.LogInformation(
                "Ingest aceito transacao={TransacaoId} status=Pendente natsArquivos={ArquivosSeq} natsDropbox={DropboxSeq} destino={Destino}",
                dispatch.Id,
                natsArquivos.Sequencia,
                natsDropbox.Sequencia,
                destino);

            return ReceberArquivoResult.Created(arquivo, dispatch, natsArquivos, natsDropbox);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Falha no ingest etapa={Etapa} nome={NomeArquivo} usuario={Usuario} bytes={Bytes} tipo={Tipo} cadeia={Cadeia}",
                etapa,
                request.NomeArquivo,
                request.Usuario,
                request.TamanhoBytes,
                ex.GetType().Name,
                LogExcecao.Cadeia(ex));

            await CompensarAsync(
                etapa,
                caminhoLocal,
                arquivo,
                dispatch,
                persistido,
                natsArquivos,
                LogExcecao.Cadeia(ex));

            return ReceberArquivoResult.Fail($"Falha na etapa {etapa}: {LogExcecao.Cadeia(ex)}");
        }
    }

    private async Task<ArquivoEventoPublicado?> PublicarDropboxComRetryAsync(
        DropboxDispatch dispatch,
        string destino,
        string caminhoLocal,
        CancellationToken cancellationToken)
    {
        Exception? ultimoErro = null;
        for (var tentativa = 1; tentativa <= MaxTentativasDropboxNats; tentativa++)
        {
            try
            {
                return await _nats.PublicarDropboxAsync(
                    new DropboxDispatchEvento(
                        dispatch.Id,
                        dispatch.ArquivoRecebidoId,
                        destino,
                        dispatch.NomeArquivo,
                        dispatch.NomeOriginal,
                        caminhoLocal),
                    cancellationToken);
            }
            catch (Exception ex)
            {
                ultimoErro = ex;
                _logger.LogWarning(
                    ex,
                    "Falha ao publicar dropbox tentativa={Tentativa}/{Max} transacao={TransacaoId} tipo={Tipo} cadeia={Cadeia}",
                    tentativa,
                    MaxTentativasDropboxNats,
                    dispatch.Id,
                    ex.GetType().Name,
                    LogExcecao.Cadeia(ex));

                if (tentativa < MaxTentativasDropboxNats)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(500 * Math.Pow(2, tentativa - 1)), cancellationToken);
                }
            }
        }

        var cadeia = ultimoErro is null ? "publicacao dropbox sem ack" : LogExcecao.Cadeia(ultimoErro);
        dispatch.RegistrarErroPublicacao($"Falha na etapa nats-dropbox: {cadeia}");
        await _dispatches.SaveChangesAsync(cancellationToken);
        _logger.LogError(
            ultimoErro,
            "Dropbox NATS nao publicado apos {Max} tentativas transacao={TransacaoId}. Arquivo mantido em {Caminho} status=Pendente",
            MaxTentativasDropboxNats,
            dispatch.Id,
            caminhoLocal);
        return null;
    }

    private async Task CompensarAsync(
        string etapa,
        string? caminhoLocal,
        ArquivoRecebido? arquivo,
        DropboxDispatch? dispatch,
        bool persistido,
        ArquivoEventoPublicado? natsArquivos,
        string cadeia)
    {
        if (natsArquivos is not null)
        {
            if (dispatch is not null && persistido)
            {
                try
                {
                    dispatch.RegistrarErroPublicacao($"Falha na etapa {etapa}: {cadeia}");
                    await _dispatches.SaveChangesAsync(CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Compensacao nao atualizou dropbox_dispatch transacao={TransacaoId}", dispatch.Id);
                }
            }

            _logger.LogWarning(
                "Compensacao ingest etapa={Etapa} arquivosPublicado=true persistido={Persistido} stagingMantido={Caminho}",
                etapa,
                persistido,
                caminhoLocal);
            return;
        }

        if (persistido && arquivo is not null)
        {
            try
            {
                if (dispatch is not null)
                {
                    _dispatches.Remove(dispatch);
                }

                _arquivos.Remove(arquivo);
                await _arquivos.SaveChangesAsync(CancellationToken.None);
                _logger.LogInformation(
                    "Compensacao ingest removeu postgres transacao={TransacaoId} etapa={Etapa}",
                    arquivo.Id,
                    etapa);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Compensacao nao removeu postgres transacao={TransacaoId} etapa={Etapa}",
                    arquivo.Id,
                    etapa);
            }
        }

        if (!string.IsNullOrWhiteSpace(caminhoLocal))
        {
            var existia = File.Exists(caminhoLocal);
            _staging.Excluir(caminhoLocal);
            _logger.LogInformation(
                "Compensacao ingest excluiu staging caminho={Caminho} etapa={Etapa} existia={Existe}",
                caminhoLocal,
                etapa,
                existia);
        }
    }
}
