using System.Net;
using AutonomousAudit.Application.Data;
using AutonomousAudit.Application.RequestHandlers;
using AutonomousAudit.Application.Security;
using AutonomousAudit.Domain;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AutonomousAudit.Api;

public static class PerplexityEndpoints
{
    public static WebApplication MapPerplexity(this WebApplication app)
    {
        app.MapPost("/v1/perplexity/chat", Consultar)
            .RequireRecurso(RecursoChaves.PerplexityPrompt)
            .Accepts<PerplexityChatBody>("application/json")
            .Produces<PerplexityChatResponse>()
            .Produces((int)HttpStatusCode.BadRequest)
            .Produces((int)HttpStatusCode.Unauthorized)
            .Produces((int)HttpStatusCode.BadGateway)
            .WithTags("Perplexity")
            .WithSummary("Pergunta à Perplexity (modelo sonar) e espera a resposta")
            .WithDescription(SwaggerDocs.Bloco(
                "Consulta síncrona à Perplexity: envie uma pergunta (e contexto opcional) e receba o texto com citações, quando a provedora informar fontes.",
                "Pesquisa assistida, checagem factual ou rascunho de resposta com referências — a tela espera o retorno neste request.",
                "- JWT + recurso `perplexity.prompt`.\n" +
                "- JSON: `pergunta` e/ou `mensagens` (papel + conteúdo); `contexto` opcional.",
                "Encaminha para `https://api.perplexity.ai/chat/completions` com o modelo sonar e devolve o texto + lista de citações.",
                "- **200** `{ modelo, resposta, citacoes }`.\n" +
                "- **400** pergunta vazia.\n" +
                "- **401** chave Perplexity inválida.\n" +
                "- **502** falha na provedora."));

        var ocr = app.MapGroup("/v1/perplexity").WithTags("Perplexity");
        ocr.MapPost("/sync", ConsultarAgente)
            .RequireRecurso(RecursoChaves.PerplexityOcr)
            .Accepts<PerplexityAgenteBody>("application/json")
            .Produces<PerplexityAgenteResponse>()
            .Produces((int)HttpStatusCode.BadRequest)
            .Produces((int)HttpStatusCode.Unauthorized)
            .Produces((int)HttpStatusCode.BadGateway)
            .WithRequestTimeout(TimeSpan.FromMinutes(6))
            .WithSummary("Analisa imagem/PDF no Perplexity Agent e espera a resposta")
            .WithDescription(SwaggerDocs.Bloco(
                "OCR multimodal via Agent API: imagem em Base64 ou PDF. PDF com texto e extraido no backend; PDF escaneado vira JPEG por pagina.",
                "Recibos, notas e documentos quando a tela pode esperar (timeout de ate 6 minutos).",
                "- JWT + recurso `perplexity.ocr`.\n" +
                "- JSON: `prompt` (ou `pergunta`), `modelo` opcional (padrao openai/gpt-5-mini), `imagemBase64` e `mimeType`.\n" +
                "- Sem web_search. Imagens redimensionadas (max 1600 px) para reduzir tokens.",
                "POST https://api.perplexity.ai/v1/agent com input_text + input_image (data URI).",
                "- **200** `{ modelo, texto, imagem, promptTokens, outputTokens, totalTokens }`.\n" +
                "- **400** arquivo/prompt invalidos.\n" +
                "- **502** falha na Agent API."));
        ocr.MapPost("/sync/arquivo", ConsultarAgenteArquivo)
            .DisableAntiforgery()
            .RequireRecurso(RecursoChaves.PerplexityOcr)
            .Accepts<PerplexityArquivoForm>("multipart/form-data")
            .Produces<PerplexityAgenteResponse>()
            .Produces((int)HttpStatusCode.BadRequest)
            .Produces((int)HttpStatusCode.Unauthorized)
            .Produces((int)HttpStatusCode.BadGateway)
            .WithRequestTimeout(TimeSpan.FromMinutes(6))
            .WithSummary("Analisa arquivo no Perplexity Agent (multipart)")
            .WithDescription(SwaggerDocs.Bloco(
                "Mesmo papel do sync JSON, com arquivo em multipart (JPEG, PNG, WEBP, GIF ou PDF).",
                "Upload de recibo real para OCR estruturado na hora.",
                "- JWT + recurso `perplexity.ocr`.\n" +
                "- `multipart/form-data`: `prompt`, `arquivo`, `modelo` e `mimeType` opcionais.",
                "Detecta MIME, extrai texto de PDF digital ou rasteriza paginas e chama a Agent API.",
                "- **200** texto extraido e tokens.\n" +
                "- **400** arquivo invalido.\n" +
                "- **502** falha na Agent API."));
        ocr.MapPost("/assync", EnfileirarPrompt)
            .RequireRecurso(RecursoChaves.PerplexityOcr)
            .Accepts<PerplexityAgenteBody>("application/json")
            .Produces<PerplexityOperacaoAceitaResponse>((int)HttpStatusCode.Accepted)
            .Produces((int)HttpStatusCode.BadRequest)
            .Produces((int)HttpStatusCode.Unauthorized)
            .WithSummary("Enfileira analise Perplexity Agent")
            .WithDescription(SwaggerDocs.Bloco(
                "Versao assincrona: grava perplexity_prompt e publica no NATS PERPLEXITY_PROMPT.",
                "Lotes e jobs longos.",
                "- JWT + recurso `perplexity.ocr`.",
                "O consumer chama a Agent API e atualiza o registro.",
                "- **202** com `id`, `status` e `statusUrl`."));
        ocr.MapGet("/assync/{id:guid}", ConsultarPrompt)
            .RequireRecurso(RecursoChaves.PerplexityOcr)
            .Produces<PerplexityPromptStatusResponse>()
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Unauthorized)
            .WithSummary("Consulta o resultado do Perplexity assincrono");
        ocr.MapPost("/assync/arquivo", EnfileirarArquivo)
            .DisableAntiforgery()
            .RequireRecurso(RecursoChaves.PerplexityOcr)
            .Accepts<PerplexityArquivoForm>("multipart/form-data")
            .Produces<PerplexityOperacaoAceitaResponse>((int)HttpStatusCode.Accepted)
            .Produces((int)HttpStatusCode.BadRequest)
            .WithSummary("Enfileira OCR Perplexity e envia o arquivo ao Dropbox")
            .WithDescription(SwaggerDocs.Bloco(
                "Grava perplexity_arquivo e publica em PERPLEXITY_ARQUIVO. O consumer analisa e inscreve o Dropbox.",
                "Quando o usuario anexa um documento e nao espera a resposta na tela.",
                "- JWT + recurso `perplexity.ocr`.",
                "Fila NATS no mesmo molde do Gemini/Qwen.",
                "- **202** com `id` e `statusUrl`."));
        ocr.MapGet("/assync/arquivo/{id:guid}", ConsultarArquivoAssincrono)
            .RequireRecurso(RecursoChaves.PerplexityOcr)
            .Produces<PerplexityArquivoStatusResponse>()
            .Produces((int)HttpStatusCode.NotFound)
            .WithSummary("Consulta OCR Perplexity assincrono com arquivo");

        return app;
    }

    private static async Task<IResult> Consultar(
        [FromBody] PerplexityChatBody body,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<PerplexityMensagem>? mensagens = body.Mensagens?
            .Select(m => new PerplexityMensagem(m.Papel ?? "user", m.Conteudo ?? string.Empty))
            .ToList();

        var result = await mediator.Send(
            new ConsultarPerplexityCommand(body.Pergunta, body.Contexto, mensagens),
            cancellationToken);

        return result switch
        {
            ConsultarPerplexityOk ok => Results.Ok(new PerplexityChatResponse(ok.Resposta.Modelo, ok.Resposta.Texto, ok.Resposta.Citacoes)),
            ConsultarPerplexityBadRequest badRequest => Results.Problem(
                title: "Requisicao invalida",
                detail: badRequest.Message,
                statusCode: StatusCodes.Status400BadRequest),
            ConsultarPerplexityUnauthorized unauthorized => Results.Problem(
                title: "Nao autorizado",
                detail: unauthorized.Message,
                statusCode: StatusCodes.Status401Unauthorized),
            ConsultarPerplexityFail failed => Results.Problem(
                title: "Falha no Perplexity",
                detail: failed.Message,
                statusCode: StatusCodes.Status502BadGateway),
            _ => throw new ArgumentOutOfRangeException(nameof(result))
        };
    }

    private static async Task<IResult> ConsultarAgente(
        HttpContext http,
        [FromBody] PerplexityAgenteBody body,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var prompt = body.Prompt ?? body.Pergunta;
        byte[]? arquivo = null;
        if (!string.IsNullOrWhiteSpace(body.ImagemBase64))
        {
            arquivo = ArquivoMime.DecodificarBase64(body.ImagemBase64);
            if (arquivo is null)
            {
                return Problem("Requisicao invalida", "imagemBase64 nao e um Base64 valido.", StatusCodes.Status400BadRequest);
            }
        }

        return await EnviarAgente(http, mediator, prompt, body.Modelo, arquivo, body.MimeType, cancellationToken);
    }

    private static async Task<IResult> ConsultarAgenteArquivo(
        HttpContext http,
        IFormFile? arquivo,
        [FromForm] string? prompt,
        [FromForm] string? pergunta,
        [FromForm] string? modelo,
        [FromForm] string? mimeType,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        byte[]? bytes = null;
        string? mime = mimeType ?? arquivo?.ContentType;
        if (arquivo is { Length: > 0 })
        {
            await using var stream = arquivo.OpenReadStream();
            await using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, cancellationToken);
            bytes = buffer.ToArray();
            mime = ArquivoMime.DetectarMime(mime, arquivo.FileName, bytes);
        }

        return await EnviarAgente(http, mediator, prompt ?? pergunta, modelo, bytes, mime, cancellationToken);
    }

    private static async Task<IResult> EnviarAgente(
        HttpContext http,
        IMediator mediator,
        string? prompt,
        string? modelo,
        byte[]? arquivo,
        string? mimeType,
        CancellationToken cancellationToken)
    {
        Guid? usuarioId = http.Items[RecursoAuthorization.HttpItemUsuarioId] is Guid id ? id : null;
        var result = await mediator.Send(
            new ConsultarPerplexityAgenteCommand(prompt, modelo, arquivo, mimeType, usuarioId),
            cancellationToken);

        return result switch
        {
            ConsultarPerplexityAgenteOk ok => Results.Ok(new PerplexityAgenteResponse(
                ok.Resposta.Modelo,
                ok.Resposta.Texto,
                ok.Resposta.Imagem,
                ok.Resposta.Uso.PromptTokens,
                ok.Resposta.Uso.OutputTokens,
                ok.Resposta.Uso.TotalTokens)),
            ConsultarPerplexityAgenteBadRequest badRequest => Problem("Requisicao invalida", badRequest.Message, StatusCodes.Status400BadRequest),
            ConsultarPerplexityAgenteUnauthorized unauthorized => Problem("Nao autorizado", unauthorized.Message, StatusCodes.Status401Unauthorized),
            ConsultarPerplexityAgenteFail failed => Problem("Falha no Perplexity", failed.Message, StatusCodes.Status502BadGateway),
            _ => throw new ArgumentOutOfRangeException(nameof(result))
        };
    }

    private static async Task<IResult> EnfileirarPrompt(
        HttpContext http,
        [FromBody] PerplexityAgenteBody body,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        if (http.Items[RecursoAuthorization.HttpItemUsuarioId] is not Guid usuarioId)
        {
            return Problem("Nao autorizado", "Usuario do token nao identificado.", StatusCodes.Status401Unauthorized);
        }

        byte[]? arquivo = null;
        if (!string.IsNullOrWhiteSpace(body.ImagemBase64))
        {
            arquivo = ArquivoMime.DecodificarBase64(body.ImagemBase64);
            if (arquivo is null)
            {
                return Problem("Requisicao invalida", "imagemBase64 nao e um Base64 valido.", StatusCodes.Status400BadRequest);
            }
        }

        var result = await mediator.Send(
            new EnfileirarPerplexityPromptCommand(usuarioId, body.Prompt ?? body.Pergunta, body.Modelo, arquivo, body.MimeType),
            cancellationToken);

        return result switch
        {
            EnfileirarPerplexityPromptAccepted ok => Results.Accepted(
                $"/v1/perplexity/assync/{ok.Operacao.Id}",
                PerplexityOperacaoAceitaResponse.Prompt(ok.Operacao, ok.Nats)),
            EnfileirarPerplexityPromptBadRequest badRequest => Problem("Requisicao invalida", badRequest.Message, StatusCodes.Status400BadRequest),
            EnfileirarPerplexityPromptFail failed => Problem("Falha ao enfileirar Perplexity", failed.Message, StatusCodes.Status502BadGateway),
            _ => throw new ArgumentOutOfRangeException(nameof(result))
        };
    }

    private static async Task<IResult> ConsultarPrompt(
        Guid id,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new ConsultarPerplexityPromptQuery(id), cancellationToken);
        return result switch
        {
            ConsultarPerplexityPromptOk ok => Results.Ok(PerplexityPromptStatusResponse.From(ok.Operacao)),
            ConsultarPerplexityPromptNotFound notFound => Problem("Nao encontrado", notFound.Message, StatusCodes.Status404NotFound),
            ConsultarPerplexityPromptFail failed => Problem("Falha ao consultar Perplexity", failed.Message, StatusCodes.Status502BadGateway),
            _ => throw new ArgumentOutOfRangeException(nameof(result))
        };
    }

    private static async Task<IResult> EnfileirarArquivo(
        HttpContext http,
        IFormFile? arquivo,
        [FromForm] string? prompt,
        [FromForm] string? pergunta,
        [FromForm] string? modelo,
        [FromForm] string? mimeType,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        if (http.Items[RecursoAuthorization.HttpItemUsuarioId] is not Guid usuarioId)
        {
            return Problem("Nao autorizado", "Usuario do token nao identificado.", StatusCodes.Status401Unauthorized);
        }

        if (arquivo is null || arquivo.Length <= 0)
        {
            return Problem("Requisicao invalida", "O arquivo e obrigatorio.", StatusCodes.Status400BadRequest);
        }

        var email = http.Items[RecursoAuthorization.HttpItemUsuarioEmail] as string ?? string.Empty;
        await using var conteudo = arquivo.OpenReadStream();
        var result = await mediator.Send(
            new EnfileirarPerplexityArquivoCommand(
                usuarioId,
                email,
                prompt ?? pergunta,
                modelo,
                arquivo.FileName,
                mimeType ?? arquivo.ContentType,
                arquivo.Length,
                conteudo),
            cancellationToken);

        return result switch
        {
            EnfileirarPerplexityArquivoAccepted ok => Results.Accepted(
                $"/v1/perplexity/assync/arquivo/{ok.Operacao.Id}",
                PerplexityOperacaoAceitaResponse.Arquivo(ok.Operacao, ok.Nats)),
            EnfileirarPerplexityArquivoBadRequest badRequest => Problem("Requisicao invalida", badRequest.Message, StatusCodes.Status400BadRequest),
            EnfileirarPerplexityArquivoFail failed => Problem("Falha ao enfileirar Perplexity", failed.Message, StatusCodes.Status502BadGateway),
            _ => throw new ArgumentOutOfRangeException(nameof(result))
        };
    }

    private static async Task<IResult> ConsultarArquivoAssincrono(
        Guid id,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new ConsultarPerplexityArquivoQuery(id), cancellationToken);
        return result switch
        {
            ConsultarPerplexityArquivoOk ok => Results.Ok(PerplexityArquivoStatusResponse.From(ok.Operacao)),
            ConsultarPerplexityArquivoNotFound notFound => Problem("Nao encontrado", notFound.Message, StatusCodes.Status404NotFound),
            ConsultarPerplexityArquivoFail failed => Problem("Falha ao consultar Perplexity", failed.Message, StatusCodes.Status502BadGateway),
            _ => throw new ArgumentOutOfRangeException(nameof(result))
        };
    }

    private static IResult Problem(string title, string detail, int status) =>
        Results.Problem(title: title, detail: detail, statusCode: status);
}

public sealed record PerplexityChatBody(
    string? Pergunta,
    string? Contexto,
    IReadOnlyList<PerplexityMensagemBody>? Mensagens);

public sealed record PerplexityMensagemBody(string? Papel, string? Conteudo);

public sealed record PerplexityChatResponse(string Modelo, string Resposta, IReadOnlyList<string> Citacoes);

public sealed record PerplexityAgenteBody(
    string? Prompt,
    string? Pergunta,
    string? Modelo,
    string? ImagemBase64,
    string? MimeType);

public sealed class PerplexityArquivoForm
{
    public string? Prompt { get; set; }
    public string? Pergunta { get; set; }
    public string? Modelo { get; set; }
    public string? MimeType { get; set; }
    public IFormFile? Arquivo { get; set; }
}

public sealed record PerplexityAgenteResponse(
    string Modelo,
    string Texto,
    bool Imagem,
    long PromptTokens,
    long OutputTokens,
    long TotalTokens);

public sealed record PerplexityOperacaoAceitaResponse(
    Guid Id,
    string Status,
    string StatusUrl,
    string NatsSubject,
    ulong NatsSequencia)
{
    public static PerplexityOperacaoAceitaResponse Prompt(PerplexityPrompt operacao, AutonomousAudit.Application.Data.ArquivoEventoPublicado nats) =>
        new(operacao.Id, operacao.Status.ToString(), $"/v1/perplexity/assync/{operacao.Id}", nats.Subject, nats.Sequencia);

    public static PerplexityOperacaoAceitaResponse Arquivo(PerplexityArquivo operacao, AutonomousAudit.Application.Data.ArquivoEventoPublicado nats) =>
        new(operacao.Id, operacao.Status.ToString(), $"/v1/perplexity/assync/arquivo/{operacao.Id}", nats.Subject, nats.Sequencia);
}

public sealed record PerplexityPromptStatusResponse(
    Guid Id,
    string Status,
    int Tentativas,
    int MaxTentativas,
    string Prompt,
    string? Modelo,
    string? ModeloResposta,
    string? Texto,
    string? UltimaMensagemErro,
    DateTimeOffset DataCriacao,
    DateTimeOffset DataAtualizacao,
    DateTimeOffset? DataConclusao)
{
    public static PerplexityPromptStatusResponse From(PerplexityPrompt operacao) =>
        new(
            operacao.Id,
            operacao.Status.ToString(),
            operacao.Tentativas,
            operacao.MaxTentativas,
            operacao.Prompt,
            operacao.Modelo,
            operacao.ModeloResposta,
            operacao.TextoResposta,
            operacao.UltimaMensagemErro,
            operacao.DataCriacao,
            operacao.DataAtualizacao,
            operacao.DataConclusao);
}

public sealed record PerplexityArquivoStatusResponse(
    Guid Id,
    string Status,
    int Tentativas,
    int MaxTentativas,
    string Prompt,
    string NomeOriginal,
    string NomeArquivo,
    string CaminhoDropbox,
    bool DropboxInscrito,
    Guid DropboxDispatchId,
    string? Modelo,
    string? ModeloResposta,
    string? Texto,
    string? UltimaMensagemErro,
    DateTimeOffset DataCriacao,
    DateTimeOffset DataAtualizacao,
    DateTimeOffset? DataConclusao)
{
    public static PerplexityArquivoStatusResponse From(PerplexityArquivo operacao) =>
        new(
            operacao.Id,
            operacao.Status.ToString(),
            operacao.Tentativas,
            operacao.MaxTentativas,
            operacao.Prompt,
            operacao.NomeOriginal,
            operacao.NomeArquivo,
            operacao.CaminhoDropbox,
            operacao.DropboxInscrito,
            operacao.DropboxDispatchId,
            operacao.Modelo,
            operacao.ModeloResposta,
            operacao.TextoResposta,
            operacao.UltimaMensagemErro,
            operacao.DataCriacao,
            operacao.DataAtualizacao,
            operacao.DataConclusao);
}
