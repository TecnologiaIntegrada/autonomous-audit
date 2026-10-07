using System.Net;
using AutonomousAudit.Application.RequestHandlers;
using AutonomousAudit.Application.Security;
using AutonomousAudit.Domain;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AutonomousAudit.Api;

public static class ArquivoEndpoints
{
    public static WebApplication MapArquivos(this WebApplication app)
    {
        app.MapPost("/v1/arquivos", ReceberArquivo)
            .DisableAntiforgery()
            .RequireRecurso(RecursoChaves.ArquivoCreate)
            .Accepts<ReceberArquivoForm>("multipart/form-data")
            .Produces<ArquivoRecebidoResponse>((int)HttpStatusCode.Created)
            .Produces((int)HttpStatusCode.BadRequest)
            .Produces((int)HttpStatusCode.BadGateway)
            .Produces((int)HttpStatusCode.RequestEntityTooLarge)
            .WithRequestTimeout(TimeSpan.FromMinutes(10))
            .WithTags("Arquivos")
            .WithSummary("Recebe um arquivo e inicia o envio assíncrono ao Dropbox")
            .WithDescription(SwaggerDocs.Bloco(
                "Porta de ingestão de documentos. O cliente envia o binário uma vez; a API confirma na hora e o Dropbox recebe o arquivo em segundo plano.",
                "Fluxos de captura (scanner, upload de NF, laudo, imagem) em que não se pode esperar o upload ao Dropbox terminar neste mesmo request.",
                "- JWT + recurso `arquivo.create`.\n" +
                "- `multipart/form-data`: campo `arquivo` (obrigatório) e `usuario` ou `email` (remetente informado).\n" +
                "- Limite típico: 100 MB (`Arquivos:MaxRequestBodyBytes`). Timeout de até 10 minutos no HTTP de entrada.",
                "1. Grava o arquivo em pasta temporária.\n" +
                "2. Registra metadados no Postgres e cria a transação `dropbox_dispatch` como Pendente.\n" +
                "3. Publica eventos NATS em **ARQUIVOS** e **dropbox**.\n" +
                "4. O consumer envia ao Dropbox em `/AutonomousAudit/{idDoUsuario}/{arquivoId}.{extensao}`.",
                "- **201** com `transacaoId`, `nomeArquivo` (nome no Dropbox) e `nomeOriginal` (nome enviado).\n" +
                "- **400** arquivo ausente ou remetente inválido.\n" +
                "- **413** arquivo acima do limite.",
                "Consulte o andamento em `GET /v1/arquivos/{transacaoId}/status` (Pendente, Enviando, Sucesso ou Falha)."));

        app.MapGet("/v1/arquivos/{transacaoId:guid}/status", ConsultarStatus)
            .RequireRecurso(RecursoChaves.ArquivoRead)
            .Produces<DropboxDispatchStatusResponse>()
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.BadGateway)
            .WithTags("Arquivos")
            .WithSummary("Acompanha o envio do arquivo ao Dropbox")
            .WithDescription(SwaggerDocs.Bloco(
                "Painel de status da transação aberta no POST /v1/arquivos: se o arquivo já chegou ao Dropbox, ainda está na fila ou falhou.",
                "Depois de receber o 201 do upload, para informar o usuário ou o processo de negócio se o documento já está na pasta.",
                "- JWT + recurso `arquivo.read`.\n" +
                "- `{transacaoId}` é o id devolvido no 201 (o mesmo da tabela `dropbox_dispatch`).",
                "Lê o registro da transação no Postgres (não consulta o Dropbox neste GET). Traz tentativas, última mensagem de erro e o caminho de destino.",
                "- **200** com status Pendente, Enviando, Sucesso ou Falha.\n" +
                "- **404** id inexistente."));

        return app;
    }

    private static async Task<IResult> ReceberArquivo(
        HttpContext http,
        IFormFile? arquivo,
        [FromForm] string? usuario,
        [FromForm] string? email,
        IMediator mediator,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("AutonomousAudit.Api.Arquivos");
        if (http.Items[RecursoAuthorization.HttpItemUsuarioId] is not Guid usuarioId)
        {
            logger.LogWarning("POST /v1/arquivos rejeitado motivo=usuario_auth_ausente trace={TraceId}", http.TraceIdentifier);
            return Results.Problem(
                title: "Nao autorizado",
                detail: "Usuario autenticado nao identificado.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        var autenticadoEmail = http.Items[RecursoAuthorization.HttpItemUsuarioEmail] as string;
        var remetente = usuario ?? email;
        var nome = arquivo?.FileName;
        var bytes = arquivo?.Length ?? 0;
        var contentType = arquivo?.ContentType ?? http.Request.ContentType;

        logger.LogInformation(
            "POST /v1/arquivos iniciado trace={TraceId} usuarioAuth={UsuarioAuth} emailAuth={EmailAuth} formUsuario={FormUsuario} nome={Nome} bytes={Bytes} contentType={ContentType} contentLength={ContentLength} hasBearer={HasBearer}",
            http.TraceIdentifier,
            usuarioId,
            autenticadoEmail,
            remetente,
            nome,
            bytes,
            contentType,
            http.Request.ContentLength,
            http.Request.Headers.Authorization.Count > 0);

        if (arquivo is null || arquivo.Length == 0)
        {
            logger.LogWarning(
                "POST /v1/arquivos rejeitado motivo=arquivo_ausente_ou_vazio trace={TraceId} formUsuario={FormUsuario} contentType={ContentType} contentLength={ContentLength}",
                http.TraceIdentifier,
                remetente,
                contentType,
                http.Request.ContentLength);
            return Results.Problem(
                title: "Requisicao invalida",
                detail: "Envie o arquivo no campo multipart 'arquivo'.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (string.IsNullOrWhiteSpace(remetente))
        {
            logger.LogWarning(
                "POST /v1/arquivos rejeitado motivo=usuario_ausente trace={TraceId} nome={Nome} bytes={Bytes}",
                http.TraceIdentifier,
                nome,
                bytes);
            return Results.Problem(
                title: "Requisicao invalida",
                detail: "Informe o e-mail do remetente no campo 'usuario'.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        await using var conteudo = arquivo.OpenReadStream();
        var result = await mediator.Send(
            new ReceberArquivoCommand(
                remetente,
                arquivo.FileName,
                arquivo.Length,
                conteudo,
                usuarioId),
            cancellationToken);

        switch (result)
        {
            case ReceberArquivoCreated created:
                logger.LogInformation(
                    "POST /v1/arquivos 201 trace={TraceId} transacao={TransacaoId} arquivoId={ArquivoId} nome={Nome} destino={Destino} natsArquivos={ArquivosSeq} natsDropbox={Seq}",
                    http.TraceIdentifier,
                    created.Dispatch.Id,
                    created.Arquivo.Id,
                    created.Arquivo.NomeArquivo,
                    created.Dispatch.CaminhoDropbox,
                    created.NatsArquivos.Sequencia,
                    created.NatsDropbox?.Sequencia);
                return Results.Created(
                    $"/v1/arquivos/{created.Dispatch.Id}/status",
                    ArquivoRecebidoResponse.From(created));
            case ReceberArquivoBadRequest badRequest:
                logger.LogWarning(
                    "POST /v1/arquivos 400 trace={TraceId} nome={Nome} usuario={Usuario} detalhe={Detalhe}",
                    http.TraceIdentifier,
                    nome,
                    remetente,
                    badRequest.Message);
                return Results.Problem(
                    title: "Requisicao invalida",
                    detail: badRequest.Message,
                    statusCode: StatusCodes.Status400BadRequest);
            case ReceberArquivoFail failed:
                var erroIngest = new InvalidOperationException(failed.Message);
                SeqErrorLog.Capturar(http, "ingest", failed.Message, exception: erroIngest);
                logger.LogError(
                    erroIngest,
                    "POST /v1/arquivos 502 trace={TraceId} nome={Nome} usuario={Usuario} detalhe={Detalhe}",
                    http.TraceIdentifier,
                    nome,
                    remetente,
                    failed.Message);
                return Results.Problem(
                    title: "Falha no ingest do arquivo",
                    detail: failed.Message,
                    statusCode: StatusCodes.Status502BadGateway);
            default:
                throw new ArgumentOutOfRangeException(nameof(result));
        }
    }

    private static async Task<IResult> ConsultarStatus(
        HttpContext http,
        Guid transacaoId,
        IMediator mediator,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("AutonomousAudit.Api.Arquivos");
        var result = await mediator.Send(new ConsultarDropboxDispatchQuery(transacaoId), cancellationToken);
        switch (result)
        {
            case ConsultarDropboxDispatchOk ok:
                logger.LogInformation(
                    "GET /v1/arquivos/{TransacaoId}/status 200 status={Status} tentativas={Tentativas} erro={Erro} trace={TraceId}",
                    transacaoId,
                    ok.Dispatch.Status,
                    ok.Dispatch.Tentativas,
                    ok.Dispatch.UltimaMensagemErro,
                    http.TraceIdentifier);
                return Results.Ok(DropboxDispatchStatusResponse.From(ok.Dispatch));
            case ConsultarDropboxDispatchNotFound notFound:
                logger.LogWarning(
                    "GET /v1/arquivos/{TransacaoId}/status 404 detalhe={Detalhe} trace={TraceId}",
                    transacaoId,
                    notFound.Message,
                    http.TraceIdentifier);
                return Results.Problem(
                    title: "Nao encontrado",
                    detail: notFound.Message,
                    statusCode: StatusCodes.Status404NotFound);
            case ConsultarDropboxDispatchFail failed:
                var erroStatus = new InvalidOperationException(failed.Message);
                SeqErrorLog.Capturar(http, "dropbox-status", failed.Message, exception: erroStatus);
                logger.LogError(
                    erroStatus,
                    "GET /v1/arquivos/{TransacaoId}/status 502 detalhe={Detalhe} trace={TraceId}",
                    transacaoId,
                    failed.Message,
                    http.TraceIdentifier);
                return Results.Problem(
                    title: "Falha ao consultar status",
                    detail: failed.Message,
                    statusCode: StatusCodes.Status502BadGateway);
            default:
                throw new ArgumentOutOfRangeException(nameof(result));
        }
    }
}

public sealed class ReceberArquivoForm
{
    public IFormFile? Arquivo { get; set; }
    public string? Usuario { get; set; }
    public string? Email { get; set; }
}

public sealed record ArquivoRecebidoResponse(
    Guid TransacaoId,
    Guid ArquivoId,
    string NomeArquivo,
    string NomeOriginal,
    decimal TamanhoMb,
    DateTimeOffset DataHora,
    string UsuarioRemetente,
    string CaminhoDropbox,
    string Status,
    string NatsSubject,
    ulong NatsSequencia)
{
    public static ArquivoRecebidoResponse From(ReceberArquivoCreated created) =>
        new(
            created.Dispatch.Id,
            created.Arquivo.Id,
            created.Dispatch.NomeArquivo,
            created.Dispatch.NomeOriginal,
            created.Arquivo.TamanhoMb,
            created.Arquivo.DataHora,
            created.Arquivo.UsuarioRemetente,
            created.Dispatch.CaminhoDropbox,
            created.Dispatch.Status.ToString(),
            created.NatsDropbox?.Subject ?? "dropbox",
            created.NatsDropbox?.Sequencia ?? 0);
}

public sealed record DropboxDispatchStatusResponse(
    Guid TransacaoId,
    Guid ArquivoId,
    string NomeArquivo,
    string NomeOriginal,
    string Status,
    int Tentativas,
    int MaxTentativas,
    string CaminhoDropbox,
    string? DropboxId,
    string? UltimaMensagemErro,
    DateTimeOffset DataCriacao,
    DateTimeOffset DataAtualizacao,
    DateTimeOffset? DataConclusao)
{
    public static DropboxDispatchStatusResponse From(DropboxDispatch dispatch) =>
        new(
            dispatch.Id,
            dispatch.ArquivoRecebidoId,
            dispatch.NomeArquivo,
            dispatch.NomeOriginal,
            dispatch.Status.ToString(),
            dispatch.Tentativas,
            dispatch.MaxTentativas,
            dispatch.CaminhoDropbox,
            dispatch.DropboxId,
            dispatch.UltimaMensagemErro,
            dispatch.DataCriacao,
            dispatch.DataAtualizacao,
            dispatch.DataConclusao);
}
