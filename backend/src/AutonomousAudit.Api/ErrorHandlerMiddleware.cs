using System.Text.Json;
using AutonomousAudit.Application;
using FluentValidation;
using Microsoft.AspNetCore.Http.Features;

namespace AutonomousAudit.Api;

public sealed class ErrorHandlerMiddleware
{
    private readonly RequestDelegate _next;

    public ErrorHandlerMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task Invoke(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleAsync(context, ex);
        }
    }

    private static async Task HandleAsync(HttpContext context, Exception ex)
    {
        if (context.RequestAborted.IsCancellationRequested && ex is OperationCanceledException)
        {
            context.Response.StatusCode = 499;
            SeqErrorLog.Capturar(context, "cancelado", "Requisicao cancelada.", exception: ex);
            SeqErrorLog.Registrar(context, null, null);
            return;
        }

        if (context.Response.HasStarted)
        {
            SeqErrorLog.Capturar(context, "resposta-iniciada", LogExcecao.Cadeia(ex), exception: ex);
            SeqErrorLog.Registrar(context, null, null);
            throw ex;
        }

        if (ex is ValidationException validation)
        {
            var detalhe = string.Join("; ", validation.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}"));
            var errors = validation.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => new
                    {
                        mensagem = e.ErrorMessage,
                        valor = ValorTentado(e.AttemptedValue)
                    }).ToArray());
            SeqErrorLog.Capturar(context, "validacao", detalhe, errors, validation);
            await WriteAsync(context, Results.ValidationProblem(
                validation.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())));
            return;
        }

        if (ex is BadHttpRequestException badRequest)
        {
            var detalhe = badRequest.InnerException is JsonException json
                ? MensagemJson(json)
                : badRequest.InnerException?.Message ?? badRequest.Message;
            SeqErrorLog.Capturar(context, "http-invalido", detalhe, exception: badRequest);
            await WriteAsync(
                context,
                Results.Problem(
                    title: "Requisicao invalida",
                    detail: detalhe,
                    statusCode: StatusCodes.Status400BadRequest));
            return;
        }

        if (ex is JsonException jsonEx)
        {
            var detalhe = MensagemJson(jsonEx);
            SeqErrorLog.Capturar(context, "json-invalido", detalhe, exception: jsonEx);
            await WriteAsync(
                context,
                Results.Problem(
                    title: "Requisicao invalida",
                    detail: detalhe,
                    statusCode: StatusCodes.Status400BadRequest));
            return;
        }

        SeqErrorLog.Capturar(context, "nao-tratado", LogExcecao.Cadeia(ex), exception: ex);
        await WriteAsync(
            context,
            Results.Problem(
                title: "Erro interno",
                detail: LogExcecao.Cadeia(ex),
                statusCode: StatusCodes.Status500InternalServerError));
    }

    private static string ValorTentado(object? valor)
    {
        var texto = Convert.ToString(valor) ?? "(null)";
        return texto.Length <= 120 ? texto : texto[..120] + "...";
    }

    private static string MensagemJson(JsonException json)
    {
        var campo = string.IsNullOrWhiteSpace(json.Path) ? "(corpo)" : json.Path;
        var posicao = json.LineNumber.HasValue && json.BytePositionInLine.HasValue
            ? $" (linha {json.LineNumber}, coluna {json.BytePositionInLine})"
            : string.Empty;
        return $"{json.Message} — Campo: {campo}{posicao}";
    }

    private static async Task WriteAsync(HttpContext context, IResult result)
    {
        context.Response.Clear();
        await result.ExecuteAsync(context);
    }
}
