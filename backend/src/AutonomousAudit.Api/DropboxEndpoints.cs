using System.Net;
using AutonomousAudit.Application.RequestHandlers;
using AutonomousAudit.Application.Security;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AutonomousAudit.Api;

public static class DropboxEndpoints
{
    public static WebApplication MapDropbox(this WebApplication app)
    {
        var group = app.MapGroup("/v1/dropbox").WithTags("Dropbox");

        group.MapGet("/conta", ObterConta)
            .RequireRecurso(RecursoChaves.DocumentoContaRead)
            .Produces<DropboxContaResponse>()
            .Produces((int)HttpStatusCode.Unauthorized)
            .Produces((int)HttpStatusCode.Forbidden)
            .Produces((int)HttpStatusCode.BadGateway)
            .WithSummary("Confere a conta Dropbox vinculada à API")
            .WithDescription(SwaggerDocs.Bloco(
                "Mostra com qual conta Dropbox a aplicação está autenticada (e-mail, nome, tipo de conta).",
                "Diagnóstico de integração: validar se o token Dropbox ainda está válido antes de enviar ou listar arquivos.",
                "- JWT + recurso `documento.conta.read`.",
                "Chama a Dropbox `users/get_current_account` com as credenciais da aplicação. Não usa pasta nem arquivo específico.",
                "- **200** dados da conta.\n" +
                "- **401/403** token Dropbox inválido ou sem escopo.\n" +
                "- **502** falha de rede ou da Dropbox."));

        group.MapGet("/arquivos", Buscar)
            .RequireRecurso(RecursoChaves.DocumentoRead)
            .Produces<IReadOnlyList<DropboxArquivoResponse>>()
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Unauthorized)
            .Produces((int)HttpStatusCode.Forbidden)
            .Produces((int)HttpStatusCode.BadGateway)
            .WithSummary("Lista ou pesquisa documentos no Dropbox")
            .WithDescription(SwaggerDocs.Bloco(
                "Consulta o que já está na pasta da aplicação no Dropbox — listagem da pasta ou busca por nome/texto.",
                "Telas de acervo, conferência se um documento chegou, ou busca por nome parcial.",
                "- JWT + recurso `documento.read`.\n" +
                "- Query `consulta` (opcional): se vazia, lista a pasta; se preenchida, pesquisa.\n" +
                "- Query `caminho` (opcional): pasta relativa dentro de `/AutonomousAudit`.",
                "Sem `consulta`, lista o diretório. Com `consulta`, usa `files/search_v2`. A pasta raiz da aplicação é `/AutonomousAudit`.",
                "- **200** lista de arquivos/pastas (nome, caminho, tamanho, data).\n" +
                "- **404** pasta inexistente."));

        group.MapPost("/arquivos", Enviar)
            .DisableAntiforgery()
            .RequireRecurso(RecursoChaves.DocumentoCreate)
            .Accepts<EnviarDropboxForm>("multipart/form-data")
            .Produces<DropboxArquivoResponse>((int)HttpStatusCode.Created)
            .Produces((int)HttpStatusCode.BadRequest)
            .Produces((int)HttpStatusCode.Unauthorized)
            .Produces((int)HttpStatusCode.Forbidden)
            .Produces((int)HttpStatusCode.BadGateway)
            .WithSummary("Envia um documento direto ao Dropbox")
            .WithDescription(SwaggerDocs.Bloco(
                "Upload síncrono: o arquivo só é confirmado depois de gravado no Dropbox neste mesmo request (diferente do POST /v1/arquivos, que usa fila).",
                "Quando o operador precisa colocar um arquivo já na pasta agora, sem transação de ingestão.",
                "- JWT + recurso `documento.create`.\n" +
                "- `multipart/form-data`: campo `arquivo` obrigatório; `caminho` opcional (subpasta dentro da pasta do usuário).",
                "Grava em `/AutonomousAudit/{idDoUsuario}/{nome}`. Se `caminho` for informado, usa essa subpasta dentro da pasta do usuário.",
                "- **201** metadados do arquivo no Dropbox.\n" +
                "- **400** arquivo ausente."));

        group.MapDelete("/arquivos", Excluir)
            .RequireRecurso(RecursoChaves.DocumentoDelete)
            .Produces<DropboxArquivoResponse>()
            .Produces((int)HttpStatusCode.BadRequest)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.Unauthorized)
            .Produces((int)HttpStatusCode.Forbidden)
            .Produces((int)HttpStatusCode.BadGateway)
            .WithSummary("Remove um documento do Dropbox")
            .WithDescription(SwaggerDocs.Bloco(
                "Exclusão definitiva de um arquivo já armazenado na conta Dropbox da aplicação.",
                "Retirar documento enviado por engano ou que não deve mais permanecer no acervo.",
                "- JWT + recurso `documento.delete`.\n" +
                "- Query `caminho` obrigatória, completa, ex.: `/AutonomousAudit/{usuarioId}/arquivo.pdf`.",
                "Chama a exclusão na Dropbox pelo caminho informado. Não apaga registros locais de ingestão.",
                "- **200** confirmação do item excluído.\n" +
                "- **400** caminho ausente.\n" +
                "- **404** arquivo não encontrado."));

        return app;
    }

    private static async Task<IResult> ObterConta(IMediator mediator, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new ObterContaDropboxQuery(), cancellationToken);
        return result switch
        {
            ObterContaDropboxOk ok => Results.Ok(DropboxContaResponse.From(ok.Conta)),
            ObterContaDropboxUnauthorized unauthorized => Problem("Nao autorizado", unauthorized.Message, StatusCodes.Status401Unauthorized),
            ObterContaDropboxForbidden forbidden => Problem("Permissao insuficiente", forbidden.Message, StatusCodes.Status403Forbidden),
            ObterContaDropboxFail failed => Problem("Falha no armazenamento", failed.Message, StatusCodes.Status502BadGateway),
            _ => throw new ArgumentOutOfRangeException(nameof(result))
        };
    }

    private static async Task<IResult> Buscar(
        string? consulta,
        string? caminho,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new BuscarDocumentosDropboxQuery(consulta, caminho), cancellationToken);
        return result switch
        {
            BuscarDocumentosDropboxOk ok => Results.Ok(ok.Itens.Select(DropboxArquivoResponse.From).ToList()),
            BuscarDocumentosDropboxUnauthorized unauthorized => Problem("Nao autorizado", unauthorized.Message, StatusCodes.Status401Unauthorized),
            BuscarDocumentosDropboxForbidden forbidden => Problem("Permissao insuficiente", forbidden.Message, StatusCodes.Status403Forbidden),
            BuscarDocumentosDropboxNotFound notFound => Problem("Nao encontrado", notFound.Message, StatusCodes.Status404NotFound),
            BuscarDocumentosDropboxFail failed => Problem("Falha no armazenamento", failed.Message, StatusCodes.Status502BadGateway),
            _ => throw new ArgumentOutOfRangeException(nameof(result))
        };
    }

    private static async Task<IResult> Enviar(
        HttpContext http,
        IFormFile? arquivo,
        [FromForm] string? caminho,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        if (http.Items[RecursoAuthorization.HttpItemUsuarioId] is not Guid usuarioId)
        {
            return Problem("Nao autorizado", "Usuario autenticado nao identificado.", StatusCodes.Status401Unauthorized);
        }

        if (arquivo is null || arquivo.Length == 0)
        {
            return Problem("Requisicao invalida", "Envie o arquivo no campo multipart 'arquivo'.", StatusCodes.Status400BadRequest);
        }

        await using var stream = arquivo.OpenReadStream();
        var result = await mediator.Send(
            new EnviarDocumentoDropboxCommand(caminho, arquivo.FileName, stream, usuarioId),
            cancellationToken);

        return result switch
        {
            EnviarDocumentoDropboxCreated created => Results.Created(
                $"/v1/dropbox/arquivos?caminho={Uri.EscapeDataString(created.Arquivo.Caminho)}",
                DropboxArquivoResponse.From(created.Arquivo)),
            EnviarDocumentoDropboxBadRequest badRequest => Problem("Requisicao invalida", badRequest.Message, StatusCodes.Status400BadRequest),
            EnviarDocumentoDropboxUnauthorized unauthorized => Problem("Nao autorizado", unauthorized.Message, StatusCodes.Status401Unauthorized),
            EnviarDocumentoDropboxForbidden forbidden => Problem("Permissao insuficiente", forbidden.Message, StatusCodes.Status403Forbidden),
            EnviarDocumentoDropboxFail failed => Problem("Falha no armazenamento", failed.Message, StatusCodes.Status502BadGateway),
            _ => throw new ArgumentOutOfRangeException(nameof(result))
        };
    }

    private static async Task<IResult> Excluir(
        string? caminho,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(caminho))
        {
            return Problem("Requisicao invalida", "Informe o caminho do arquivo no armazenamento.", StatusCodes.Status400BadRequest);
        }

        var result = await mediator.Send(new ExcluirDocumentoDropboxCommand(caminho), cancellationToken);
        return result switch
        {
            ExcluirDocumentoDropboxOk ok => Results.Ok(DropboxArquivoResponse.From(ok.Arquivo)),
            ExcluirDocumentoDropboxBadRequest badRequest => Problem("Requisicao invalida", badRequest.Message, StatusCodes.Status400BadRequest),
            ExcluirDocumentoDropboxUnauthorized unauthorized => Problem("Nao autorizado", unauthorized.Message, StatusCodes.Status401Unauthorized),
            ExcluirDocumentoDropboxForbidden forbidden => Problem("Permissao insuficiente", forbidden.Message, StatusCodes.Status403Forbidden),
            ExcluirDocumentoDropboxNotFound notFound => Problem("Nao encontrado", notFound.Message, StatusCodes.Status404NotFound),
            ExcluirDocumentoDropboxFail failed => Problem("Falha no armazenamento", failed.Message, StatusCodes.Status502BadGateway),
            _ => throw new ArgumentOutOfRangeException(nameof(result))
        };
    }

    private static IResult Problem(string title, string detail, int status) =>
        Results.Problem(title: title, detail: detail, statusCode: status);
}

public sealed class EnviarDropboxForm
{
    public IFormFile? Arquivo { get; set; }
    public string? Caminho { get; set; }
}

public sealed record DropboxContaResponse(
    string Email,
    string Nome,
    string TipoConta,
    string Pais,
    string Locale,
    string AccountId,
    bool EmailVerificado)
{
    public static DropboxContaResponse From(AutonomousAudit.Application.Data.DropboxContaAtual conta) =>
        new(conta.Email, conta.Nome, conta.TipoConta, conta.Pais, conta.Locale, conta.AccountId, conta.EmailVerificado);
}

public sealed record DropboxArquivoResponse(
    string Nome,
    string Caminho,
    string? Id,
    long? TamanhoBytes,
    DateTimeOffset? ModificadoEm,
    string Tipo)
{
    public static DropboxArquivoResponse From(AutonomousAudit.Application.Data.DropboxEntrada entrada) =>
        new(entrada.Nome, entrada.Caminho, entrada.Id, entrada.TamanhoBytes, entrada.ModificadoEm, entrada.Tipo);
}
