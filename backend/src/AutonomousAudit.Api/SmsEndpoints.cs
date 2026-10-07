using System.Net;
using AutonomousAudit.Application.RequestHandlers;
using AutonomousAudit.Application.Security;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AutonomousAudit.Api;

public static class SmsEndpoints
{
    public static WebApplication MapSms(this WebApplication app)
    {
        var group = app.MapGroup("/v1/sms").WithTags("SMS");

        group.MapPost("", Enviar)
            .RequireRecurso(RecursoChaves.SmsSend)
            .Produces<SmsEnviadoResponse>((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.BadRequest)
            .Produces((int)HttpStatusCode.Unauthorized)
            .Produces((int)HttpStatusCode.BadGateway)
            .WithSummary("Envia um SMS pela SMSDev")
            .WithDescription(SwaggerDocs.Bloco(
                "Disparo imediato de SMS para um número brasileiro. A API chama a SMSDev neste request e devolve o id da mensagem.",
                "Avisos operacionais, códigos avulsos ou notificações curtas (o MFA de login usa outro fluxo interno).",
                "- JWT + recurso `sms.send`.\n" +
                "- JSON `{ \"numero\": \"+5511999998888\", \"mensagem\": \"texto\", \"referencia\": \"opcional\" }`.\n" +
                "- Também aceita form-urlencoded e o JSON sem aspas que o curl.exe do PowerShell às vezes gera.",
                "Normaliza o número, envia à SMSDev e devolve a situação/código da provedora.",
                "- **200** `{ situacao, codigo, id, numero, descricao }` — use o `id` no GET de status.\n" +
                "- **400** número ou mensagem inválidos.\n" +
                "- **502** falha na SMSDev.",
                atencao: "No PowerShell, prefira aspas simples no `-d` do curl para o JSON não quebrar."));

        group.MapGet("/{id}", Consultar)
            .RequireRecurso(RecursoChaves.SmsStatusRead)
            .Produces<SmsStatusResponse>()
            .Produces((int)HttpStatusCode.BadRequest)
            .Produces((int)HttpStatusCode.Unauthorized)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces((int)HttpStatusCode.BadGateway)
            .WithSummary("Consulta se o SMS foi entregue")
            .WithDescription(SwaggerDocs.Bloco(
                "Confere na SMSDev o DLR (relatório de entrega) de um SMS já enviado: se foi para a operadora, se chegou, se falhou.",
                "Acompanhamento após o POST /v1/sms, usando o `id` devolvido.",
                "- JWT + recurso `sms.status.read`.\n" +
                "- `{id}` é o identificador da SMSDev, não um GUID interno.",
                "Consulta `https://api.smsdev.com.br/v1/dlr`. Traz situação, data de envio, operadora e descrição.",
                "- **200** status de entrega.\n" +
                "- **404** id desconhecido na SMSDev.\n" +
                "- **502** falha na consulta."));

        return app;
    }

    private static async Task<IResult> Enviar(
        HttpContext http,
        IMediator mediator,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("AutonomousAudit.Api.Sms");
        var (body, origem, erro) = await SmsBodyLeitor.LerAsync(http.Request, cancellationToken);
        if (body is null)
        {
            var detalhe = erro ?? "Corpo ausente ou JSON invalido.";
            logger.LogWarning(
                "POST /v1/sms 400 motivo={Origem} detalhe={Detalhe} contentType={ContentType} contentLength={ContentLength} trace={TraceId}",
                origem,
                detalhe,
                http.Request.ContentType,
                http.Request.ContentLength,
                http.TraceIdentifier);
            SeqErrorLog.Capturar(http, "corpo-invalido", detalhe);
            return Problem("Requisicao invalida", detalhe, StatusCodes.Status400BadRequest);
        }

        var numeroInformado = body.Numero ?? string.Empty;
        var numeroDigitos = SmsNumero.Normalizar(numeroInformado);
        logger.LogInformation(
            "POST /v1/sms iniciado origem={Origem} numero={NumeroSufixo} digitos={Digitos} charsMsg={Chars} referencia={Referencia} contentType={ContentType} contentLength={ContentLength} trace={TraceId}",
            origem,
            SmsNumero.Sufixo(numeroDigitos),
            SmsNumero.QuantidadeDigitos(numeroDigitos),
            body.Mensagem?.Length ?? 0,
            body.Referencia,
            http.Request.ContentType,
            http.Request.ContentLength,
            http.TraceIdentifier);

        var result = await mediator.Send(
            new EnviarSmsCommand(numeroInformado, body.Mensagem ?? string.Empty, body.Referencia),
            cancellationToken);

        switch (result)
        {
            case EnviarSmsOk ok:
                logger.LogInformation(
                    "POST /v1/sms 200 situacao={Situacao} codigoSmsDev={Codigo} id={Id} numero={Numero} descricao={Descricao} trace={TraceId}",
                    ok.Envio.Situacao,
                    ok.Envio.Codigo,
                    ok.Envio.Id,
                    ok.Envio.Numero,
                    ok.Envio.Descricao,
                    http.TraceIdentifier);
                return Results.Ok(SmsEnviadoResponse.From(ok.Envio));
            case EnviarSmsBadRequest badRequest:
                logger.LogWarning(
                    "POST /v1/sms 400 numero={NumeroSufixo} digitos={Digitos} detalhe={Detalhe} trace={TraceId}",
                    SmsNumero.Sufixo(numeroDigitos),
                    SmsNumero.QuantidadeDigitos(numeroDigitos),
                    badRequest.Message,
                    http.TraceIdentifier);
                SeqErrorLog.Capturar(http, "validacao", badRequest.Message);
                return Problem("Requisicao invalida", badRequest.Message, StatusCodes.Status400BadRequest);
            case EnviarSmsUnauthorized unauthorized:
                logger.LogWarning(
                    "POST /v1/sms 401 detalhe={Detalhe} trace={TraceId}",
                    unauthorized.Message,
                    http.TraceIdentifier);
                SeqErrorLog.Capturar(http, "nao-autorizado", unauthorized.Message);
                return Problem("Nao autorizado", unauthorized.Message, StatusCodes.Status401Unauthorized);
            case EnviarSmsFail failed:
                var erroSms = new InvalidOperationException(failed.Message);
                logger.LogError(
                    erroSms,
                    "POST /v1/sms 502 numero={NumeroSufixo} digitos={Digitos} detalhe={Detalhe} trace={TraceId}",
                    SmsNumero.Sufixo(numeroDigitos),
                    SmsNumero.QuantidadeDigitos(numeroDigitos),
                    failed.Message,
                    http.TraceIdentifier);
                SeqErrorLog.Capturar(http, "smsdev", failed.Message, exception: erroSms);
                return Problem("Falha no envio de SMS", failed.Message, StatusCodes.Status502BadGateway);
            default:
                throw new ArgumentOutOfRangeException(nameof(result));
        }
    }

    private static async Task<IResult> Consultar(
        HttpContext http,
        string id,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new ConsultarSmsQuery(id), cancellationToken);
        switch (result)
        {
            case ConsultarSmsOk ok:
                return Results.Ok(SmsStatusResponse.From(ok.Status));
            case ConsultarSmsBadRequest badRequest:
                SeqErrorLog.Capturar(http, "validacao", badRequest.Message);
                return Problem("Requisicao invalida", badRequest.Message, StatusCodes.Status400BadRequest);
            case ConsultarSmsUnauthorized unauthorized:
                SeqErrorLog.Capturar(http, "nao-autorizado", unauthorized.Message);
                return Problem("Nao autorizado", unauthorized.Message, StatusCodes.Status401Unauthorized);
            case ConsultarSmsNotFound notFound:
                SeqErrorLog.Capturar(http, "nao-encontrado", notFound.Message);
                return Problem("Nao encontrado", notFound.Message, StatusCodes.Status404NotFound);
            case ConsultarSmsFail failed:
                var erroConsulta = new InvalidOperationException(failed.Message);
                SeqErrorLog.Capturar(http, "smsdev", failed.Message, exception: erroConsulta);
                return Problem("Falha na consulta de SMS", failed.Message, StatusCodes.Status502BadGateway);
            default:
                throw new ArgumentOutOfRangeException(nameof(result));
        }
    }

    private static IResult Problem(string title, string detail, int status) =>
        Results.Problem(title: title, detail: detail, statusCode: status);
}

public sealed record EnviarSmsBody(string? Numero, string? Mensagem, string? Referencia);

public sealed record SmsEnviadoResponse(string Situacao, string Codigo, string Id, string Numero, string Descricao)
{
    public static SmsEnviadoResponse From(AutonomousAudit.Application.Data.SmsEnvioResultado envio) =>
        new(envio.Situacao, envio.Codigo, envio.Id, envio.Numero, envio.Descricao);
}

public sealed record SmsStatusResponse(
    string Situacao,
    string Codigo,
    string Id,
    string? DataEnvio,
    string? Operadora,
    string Descricao)
{
    public static SmsStatusResponse From(AutonomousAudit.Application.Data.SmsStatusResultado status) =>
        new(status.Situacao, status.Codigo, status.Id, status.DataEnvio, status.Operadora, status.Descricao);
}
