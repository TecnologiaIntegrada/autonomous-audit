using System.Net;
using AutonomousAudit.Application.Data;
using AutonomousAudit.Application.RequestHandlers;
using AutonomousAudit.Application.Security;
using AutonomousAudit.Domain;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AutonomousAudit.Api;

public static class EmailEndpoints
{
    public static WebApplication MapEmails(this WebApplication app)
    {
        var group = app.MapGroup("/v1/emails").WithTags("E-mail");

        group.MapPost("", Enviar)
            .RequireRecurso(RecursoChaves.EmailSend)
            .Accepts<EnviarEmailBody>("application/json")
            .Produces<EmailEnfileiradoResponse>((int)HttpStatusCode.Created)
            .Produces((int)HttpStatusCode.BadRequest)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.BadGateway)
            .WithSummary("Enfileira um e-mail sem anexo")
            .WithDescription(SwaggerDocs.Bloco(
                "Dispara uma mensagem de e-mail **sem arquivo**. A API não fala com o SMTP neste request: ela registra o pedido e coloca na fila.",
                "Comunicados, avisos e mensagens HTML/texto para um ou mais destinatários, quando não há anexo.",
                "- JWT + recurso `email.send`.\n" +
                "- JSON: `contaId` (id em mail_accounts), `para`, `assunto`, `corpo`, `html` (true = HTML, false = texto puro).\n" +
                "- `para`: um e-mail ou vários separados por ponto e vírgula.",
                "1. Usa a conta SMTP indicada em `contaId` (servidor, porta, usuário, senha cifrada, TLS).\n" +
                "2. O campo `remetente` da conta assina o From e o Reply-To (para onde vai a resposta ao clicar em Responder).\n" +
                "3. Grava `mail_log` como Pendente (sem o corpo da mensagem) e publica no NATS **MAIL**.\n" +
                "4. O consumer envia pelo SMTP e atualiza o log.",
                "- **201** com `id`, `statusUrl`, From, To, assunto e sequência NATS.\n" +
                "- **400** destinatário/assunto/corpo inválidos.\n" +
                "- **404** conta SMTP inexistente.",
                "Acompanhe em `GET /v1/emails/{id}`. O corpo viaja só na fila, não fica no banco."));

        group.MapPost("/mensagens", EnviarMensagem)
            .DisableAntiforgery()
            .RequireRecurso(RecursoChaves.EmailSend)
            .Accepts<EnviarMensagemEmailForm>("multipart/form-data")
            .Produces<EmailEnfileiradoResponse>((int)HttpStatusCode.Created)
            .Produces((int)HttpStatusCode.BadRequest)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.BadGateway)
            .WithSummary("Enfileira um e-mail com um único anexo")
            .WithDescription(SwaggerDocs.Bloco(
                "Igual ao POST /v1/emails, porém com **um anexo obrigatório**. O arquivo é temporário: entra, segue na mensagem e é apagado — inclusive se o SMTP falhar.",
                "Boletos, comprovantes, imagens ou PDFs que precisam ir juntos da mensagem.",
                "- JWT + recurso `email.send`.\n" +
                "- `multipart/form-data`: `contaId`, `para`, `assunto`, `corpo`, `html`, `arquivo`.\n" +
                "- Um destinatário ou vários separados por ponto e vírgula. Anexo máximo: 25 MB.",
                "1. Grava o anexo em pasta temp.\n" +
                "2. Cria `mail_log` (nome do anexo, sem o conteúdo do body) e publica em **MAIL**.\n" +
                "3. O consumer anexa o arquivo, envia o SMTP e **apaga** o temp em sucesso ou falha.",
                "- **201** com `id` e `nomeAnexo`.\n" +
                "- **400** sem arquivo, arquivo vazio ou campos obrigatórios ausentes.\n" +
                "- **404** conta SMTP inexistente.",
                "Consulte `GET /v1/emails/{id}`.",
                "No curl, envie o HTML com `-F \"corpo=<C:\\caminho\\mensagem.html\"` para o `;` do CSS não quebrar o formulário. O anexo usa `@arquivo`."));

        group.MapGet("/contas", ListarContas)
            .RequireRecurso(RecursoChaves.EmailContaRead)
            .Produces<IReadOnlyList<ContaEmailResponse>>()
            .Produces((int)HttpStatusCode.BadGateway)
            .WithSummary("Lista as contas SMTP cadastradas")
            .WithDescription(SwaggerDocs.Bloco(
                "Inventário das contas usadas para enviar e-mail. Cada item é uma linha de `mail_accounts`.",
                "Escolher o `contaId` certo antes de enfileirar uma mensagem, ou auditar quais SMTP estão configurados.",
                "- JWT + recurso `email.conta.read`.",
                "Lista e-mail SMTP, remetente (From/Reply-To), servidor, portas e modo TLS. **A senha nunca é devolvida.**",
                "- **200** lista de contas."));

        group.MapPost("/contas", CriarConta)
            .RequireRecurso(RecursoChaves.EmailContaCreate)
            .Accepts<CriarContaEmailBody>("application/json")
            .Produces<ContaEmailResponse>((int)HttpStatusCode.Created)
            .Produces((int)HttpStatusCode.BadRequest)
            .Produces((int)HttpStatusCode.BadGateway)
            .WithSummary("Cadastra uma conta SMTP")
            .WithDescription(SwaggerDocs.Bloco(
                "Inclui um novo remetente técnico: servidor, porta, usuário SMTP e senha. Essa conta passa a poder ser usada nos POSTs de envio.",
                "Onboarding de um cliente ou de uma caixa institucional que a API vai usar para despachar mensagens.",
                "- JWT + recurso `email.conta.create`.\n" +
                "- JSON: `email`, `senha`, `servidor`, `portaSmtp`, `portaPop` (opcional), `protocolo`, `modoTls` (`ssl`, `starttls` ou `none`), `remetente` (opcional).",
                "A senha é cifrada (AES-GCM) antes de gravar. Se `remetente` for omitido, a API usa o próprio `email` SMTP como From/Reply-To.",
                "- **201** conta criada (sem senha).\n" +
                "- **400** dados inválidos."));

        group.MapPut("/contas/{id:guid}/senha", AtualizarSenha)
            .RequireRecurso(RecursoChaves.EmailContaUpdate)
            .Accepts<AtualizarSenhaContaEmailBody>("application/json")
            .Produces<ContaEmailResponse>()
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.BadRequest)
            .Produces((int)HttpStatusCode.BadGateway)
            .WithSummary("Troca a senha de uma conta SMTP")
            .WithDescription(SwaggerDocs.Bloco(
                "Atualiza só a senha SMTP de uma conta já cadastrada, sem recriar o restante da configuração.",
                "Quando a senha da caixa foi alterada no provedor (Titan, Google, etc.) e os envios passaram a falhar.",
                "- JWT + recurso `email.conta.update`.\n" +
                "- JSON `{ \"senha\": \"...\" }`.",
                "Cifra a nova senha e substitui o valor em `mail_accounts`. A senha não volta na resposta.",
                "- **200** conta atualizada (sem senha).\n" +
                "- **404** conta inexistente."));

        group.MapGet("/{id:guid}", ConsultarEnvio)
            .RequireRecurso(RecursoChaves.EmailSend)
            .Produces<MailLogStatusResponse>()
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.BadGateway)
            .WithSummary("Consulta o andamento de um e-mail enfileirado")
            .WithDescription(SwaggerDocs.Bloco(
                "Mostra se a mensagem já saiu, ainda está na fila ou falhou no SMTP — sem devolver o corpo da mensagem.",
                "Depois do 201 dos POSTs de e-mail, para a tela de acompanhamento ou para suporte.",
                "- JWT + recurso `email.send`.\n" +
                "- `{id}` é o GUID devolvido no 201.",
                "Lê `mail_log`: From, To, assunto, data, `jti` de quem enviou, nome do anexo (se houve), tentativas, resposta do servidor SMTP e status (Pendente, Processando, Sucesso, Falha). O body **não** é retornado.",
                "- **200** detalhes e `sucesso` true/false.\n" +
                "- **404** id inexistente."));

        return app;
    }

    private static async Task<IResult> Enviar(
        HttpContext http,
        [FromBody] EnviarEmailBody body,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        if (http.Items[RecursoAuthorization.HttpItemUsuarioId] is not Guid usuarioId)
        {
            return Problem("Nao autorizado", "Usuario do token nao identificado.", StatusCodes.Status401Unauthorized);
        }

        var jti = http.Items[RecursoAuthorization.HttpItemUsuarioJti] as string ?? string.Empty;
        var result = await mediator.Send(
            new EnviarEmailCommand(
                usuarioId,
                jti,
                body.ContaId,
                body.Para ?? string.Empty,
                body.Assunto ?? string.Empty,
                body.Corpo ?? string.Empty,
                body.Html),
            cancellationToken);

        return result switch
        {
            EnviarEmailCreated created => Results.Created(
                $"/v1/emails/{created.Log.Id}",
                EmailEnfileiradoResponse.From(created.Log, created.Nats)),
            EnviarEmailBadRequest badRequest => Problem("Requisicao invalida", badRequest.Message, StatusCodes.Status400BadRequest),
            EnviarEmailNotFound notFound => Problem("Nao encontrado", notFound.Message, StatusCodes.Status404NotFound),
            EnviarEmailFail failed => Problem("Falha ao enfileirar e-mail", failed.Message, StatusCodes.Status502BadGateway),
            _ => throw new ArgumentOutOfRangeException(nameof(result))
        };
    }

    private static async Task<IResult> EnviarMensagem(
        HttpContext http,
        IFormFile? arquivo,
        [FromForm] Guid contaId,
        [FromForm] string? para,
        [FromForm] string? assunto,
        [FromForm] string? corpo,
        [FromForm] bool html,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        if (http.Items[RecursoAuthorization.HttpItemUsuarioId] is not Guid usuarioId)
        {
            return Problem("Nao autorizado", "Usuario do token nao identificado.", StatusCodes.Status401Unauthorized);
        }

        if (arquivo is null || arquivo.Length <= 0)
        {
            return Problem("Requisicao invalida", "O arquivo anexo e obrigatorio.", StatusCodes.Status400BadRequest);
        }

        var jti = http.Items[RecursoAuthorization.HttpItemUsuarioJti] as string ?? string.Empty;
        await using var conteudo = arquivo.OpenReadStream();
        var result = await mediator.Send(
            new EnviarMensagemEmailCommand(
                usuarioId,
                jti,
                contaId,
                para ?? string.Empty,
                assunto ?? string.Empty,
                corpo ?? string.Empty,
                html,
                arquivo.FileName,
                arquivo.Length,
                conteudo),
            cancellationToken);

        return result switch
        {
            EnviarMensagemEmailCreated created => Results.Created(
                $"/v1/emails/{created.Log.Id}",
                EmailEnfileiradoResponse.From(created.Log, created.Nats)),
            EnviarMensagemEmailBadRequest badRequest => Problem("Requisicao invalida", badRequest.Message, StatusCodes.Status400BadRequest),
            EnviarMensagemEmailNotFound notFound => Problem("Nao encontrado", notFound.Message, StatusCodes.Status404NotFound),
            EnviarMensagemEmailFail failed => Problem("Falha ao enfileirar e-mail", failed.Message, StatusCodes.Status502BadGateway),
            _ => throw new ArgumentOutOfRangeException(nameof(result))
        };
    }

    private static async Task<IResult> ConsultarEnvio(Guid id, IMediator mediator, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new ConsultarMailLogQuery(id), cancellationToken);
        return result switch
        {
            ConsultarMailLogOk ok => Results.Ok(MailLogStatusResponse.From(ok.Log)),
            ConsultarMailLogNotFound notFound => Problem("Nao encontrado", notFound.Message, StatusCodes.Status404NotFound),
            ConsultarMailLogFail failed => Problem("Falha ao consultar e-mail", failed.Message, StatusCodes.Status502BadGateway),
            _ => throw new ArgumentOutOfRangeException(nameof(result))
        };
    }

    private static async Task<IResult> ListarContas(IMediator mediator, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new ListarContasEmailQuery(), cancellationToken);
        return result switch
        {
            ListarContasEmailOk ok => Results.Ok(ok.Contas.Select(ContaEmailResponse.From).ToList()),
            ListarContasEmailFail failed => Problem("Falha ao listar contas", failed.Message, StatusCodes.Status502BadGateway),
            _ => throw new ArgumentOutOfRangeException(nameof(result))
        };
    }

    private static async Task<IResult> CriarConta(
        [FromBody] CriarContaEmailBody body,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new CriarContaEmailCommand(
                body.Email ?? string.Empty,
                body.Senha ?? string.Empty,
                body.Servidor ?? string.Empty,
                body.PortaSmtp,
                body.PortaPop,
                body.Protocolo,
                body.ModoTls,
                body.Remetente),
            cancellationToken);

        return result switch
        {
            CriarContaEmailCreated created => Results.Created(
                $"/v1/emails/contas/{created.Conta.Id}",
                ContaEmailResponse.From(created.Conta)),
            CriarContaEmailBadRequest badRequest => Problem("Requisicao invalida", badRequest.Message, StatusCodes.Status400BadRequest),
            CriarContaEmailFail failed => Problem("Falha ao gravar a conta", failed.Message, StatusCodes.Status502BadGateway),
            _ => throw new ArgumentOutOfRangeException(nameof(result))
        };
    }

    private static async Task<IResult> AtualizarSenha(
        Guid id,
        [FromBody] AtualizarSenhaContaEmailBody body,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new AtualizarSenhaContaEmailCommand(id, body.Senha ?? string.Empty),
            cancellationToken);

        return result switch
        {
            AtualizarSenhaContaEmailOk ok => Results.Ok(ContaEmailResponse.From(ok.Conta)),
            AtualizarSenhaContaEmailNotFound notFound => Problem("Nao encontrado", notFound.Message, StatusCodes.Status404NotFound),
            AtualizarSenhaContaEmailBadRequest badRequest => Problem("Requisicao invalida", badRequest.Message, StatusCodes.Status400BadRequest),
            AtualizarSenhaContaEmailFail failed => Problem("Falha ao atualizar a senha", failed.Message, StatusCodes.Status502BadGateway),
            _ => throw new ArgumentOutOfRangeException(nameof(result))
        };
    }

    private static IResult Problem(string title, string detail, int status) =>
        Results.Problem(title: title, detail: detail, statusCode: status);
}

public sealed record EnviarEmailBody(
    Guid ContaId,
    string? Para,
    string? Assunto,
    string? Corpo,
    bool Html = false);

public sealed class EnviarMensagemEmailForm
{
    public Guid ContaId { get; set; }
    public string? Para { get; set; }
    public string? Assunto { get; set; }
    public string? Corpo { get; set; }
    public bool Html { get; set; }
    public IFormFile? Arquivo { get; set; }
}

public sealed record EmailEnfileiradoResponse(
    Guid Id,
    string Status,
    string StatusUrl,
    string De,
    string Para,
    string Assunto,
    string? NomeAnexo,
    string NatsSubject,
    ulong NatsSequencia)
{
    public static EmailEnfileiradoResponse From(MailLog log, ArquivoEventoPublicado nats) =>
        new(
            log.Id,
            log.Status.ToString(),
            $"/v1/emails/{log.Id}",
            log.De,
            log.Para,
            log.Assunto,
            log.NomeAnexo,
            nats.Subject,
            nats.Sequencia);
}

public sealed record MailLogStatusResponse(
    Guid Id,
    Guid ContaId,
    string Status,
    bool Sucesso,
    string De,
    string Para,
    string Assunto,
    string? NomeAnexo,
    string UsuarioToken,
    string? RespostaServidor,
    string? UltimaMensagemErro,
    int Tentativas,
    int MaxTentativas,
    DateTimeOffset DataCriacao,
    DateTimeOffset DataAtualizacao,
    DateTimeOffset? DataEnvio)
{
    public static MailLogStatusResponse From(MailLog log) =>
        new(
            log.Id,
            log.ContaId,
            log.Status.ToString(),
            log.Status == MailLogStatus.Sucesso,
            log.De,
            log.Para,
            log.Assunto,
            log.NomeAnexo,
            log.UsuarioToken,
            log.RespostaServidor,
            log.UltimaMensagemErro,
            log.Tentativas,
            log.MaxTentativas,
            log.DataCriacao,
            log.DataAtualizacao,
            log.DataEnvio);
}

public sealed record AtualizarSenhaContaEmailBody(string? Senha);

public sealed record CriarContaEmailBody(
    string? Email,
    string? Senha,
    string? Servidor,
    int PortaSmtp,
    int? PortaPop,
    string? Protocolo,
    string? ModoTls,
    string? Remetente);

public sealed record ContaEmailResponse(
    Guid Id,
    string Email,
    string Remetente,
    string Servidor,
    int PortaSmtp,
    int? PortaPop,
    string Protocolo,
    string ModoTls)
{
    public static ContaEmailResponse From(ContaEmail conta) =>
        new(
            conta.Id,
            conta.Email,
            conta.RemetenteEfetivo,
            conta.Servidor,
            conta.PortaSmtp,
            conta.PortaPop,
            conta.Protocolo,
            conta.ModoTls);
}
