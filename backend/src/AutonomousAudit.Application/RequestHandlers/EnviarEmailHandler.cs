using AutonomousAudit.Application.Data;
using AutonomousAudit.Domain;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AutonomousAudit.Application.RequestHandlers;

public record EnviarEmailCommand(
    Guid UsuarioId,
    string UsuarioToken,
    Guid ContaId,
    string Para,
    string Assunto,
    string Corpo,
    bool Html) : ICommand<EnviarEmailResult>;

public abstract record EnviarEmailResult
{
    public static EnviarEmailResult Created(MailLog log, ArquivoEventoPublicado nats) =>
        new EnviarEmailCreated(log, nats);

    public static EnviarEmailResult BadRequest(string message) => new EnviarEmailBadRequest(message);
    public static EnviarEmailResult NotFound(string message) => new EnviarEmailNotFound(message);
    public static EnviarEmailResult Fail(string message) => new EnviarEmailFail(message);
}

public record EnviarEmailCreated(MailLog Log, ArquivoEventoPublicado Nats) : EnviarEmailResult;
public record EnviarEmailBadRequest(string Message) : EnviarEmailResult;
public record EnviarEmailNotFound(string Message) : EnviarEmailResult;
public record EnviarEmailFail(string Message) : EnviarEmailResult;

public sealed class EnviarEmailCommandValidator : AbstractValidator<EnviarEmailCommand>
{
    public EnviarEmailCommandValidator()
    {
        RuleFor(x => x.UsuarioId).NotEmpty();
        RuleFor(x => x.ContaId).NotEmpty().WithMessage("Informe contaId da tabela mail_accounts.");
        RuleFor(x => x.Para)
            .NotEmpty().WithMessage("Informe o destinatario no campo 'para'.");
        RuleFor(x => x.Assunto)
            .NotEmpty().WithMessage("Informe o assunto.")
            .MaximumLength(500);
        RuleFor(x => x.Corpo)
            .NotEmpty().WithMessage("Informe o corpo do e-mail.");
    }
}

public sealed class EnviarEmailHandler : IRequestHandler<EnviarEmailCommand, EnviarEmailResult>
{
    private readonly IContaEmailRepository _contas;
    private readonly IMailLogRepository _logs;
    private readonly IArquivoEventoPublisher _nats;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<EnviarEmailHandler> _logger;

    public EnviarEmailHandler(
        IContaEmailRepository contas,
        IMailLogRepository logs,
        IArquivoEventoPublisher nats,
        IUnitOfWork uow,
        ILogger<EnviarEmailHandler> logger)
    {
        _contas = contas;
        _logs = logs;
        _nats = nats;
        _uow = uow;
        _logger = logger;
    }

    public async Task<EnviarEmailResult> Handle(EnviarEmailCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var destinatarios = EmailDestinatarios.Parse(request.Para);
            if (destinatarios.Count == 0)
            {
                return EnviarEmailResult.BadRequest(
                    "Informe ao menos um e-mail valido em 'para', separado por ponto e virgula.");
            }

            var conta = await _contas.GetByIdAsync(request.ContaId, cancellationToken);
            if (conta is null)
            {
                return EnviarEmailResult.NotFound("Conta de e-mail nao encontrada.");
            }

            var log = new MailLog(
                conta.Id,
                request.UsuarioId,
                request.UsuarioToken,
                conta.RemetenteEfetivo,
                EmailDestinatarios.Juntar(destinatarios),
                request.Assunto.Trim(),
                nomeAnexo: null);

            await _logs.AddAsync(log, cancellationToken);
            await _logs.SaveChangesAsync(cancellationToken);
            await _uow.CommitAsync(cancellationToken);

            var publicado = await _nats.PublicarMailAsync(
                new MailEnvioEvento(log.Id, request.Corpo, request.Html, null, null),
                cancellationToken);

            _logger.LogInformation(
                "E-mail enfileirado id={Id} conta={ContaId} de={De} para={Para} html={Html} seq={Seq}",
                log.Id,
                conta.Id,
                log.De,
                log.Para,
                request.Html,
                publicado.Sequencia);
            return EnviarEmailResult.Created(log, publicado);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao enfileirar e-mail conta={ContaId}", request.ContaId);
            return EnviarEmailResult.Fail(ex.Message);
        }
    }
}

public record ConsultarMailLogQuery(Guid Id) : IQuery<ConsultarMailLogResult>;

public abstract record ConsultarMailLogResult
{
    public static ConsultarMailLogResult Ok(MailLog log) => new ConsultarMailLogOk(log);
    public static ConsultarMailLogResult NotFound(string message) => new ConsultarMailLogNotFound(message);
    public static ConsultarMailLogResult Fail(string message) => new ConsultarMailLogFail(message);
}

public record ConsultarMailLogOk(MailLog Log) : ConsultarMailLogResult;
public record ConsultarMailLogNotFound(string Message) : ConsultarMailLogResult;
public record ConsultarMailLogFail(string Message) : ConsultarMailLogResult;

public sealed class ConsultarMailLogQueryValidator : AbstractValidator<ConsultarMailLogQuery>
{
    public ConsultarMailLogQueryValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public sealed class ConsultarMailLogHandler : IRequestHandler<ConsultarMailLogQuery, ConsultarMailLogResult>
{
    private readonly IMailLogRepository _logs;
    private readonly ILogger<ConsultarMailLogHandler> _logger;

    public ConsultarMailLogHandler(IMailLogRepository logs, ILogger<ConsultarMailLogHandler> logger)
    {
        _logs = logs;
        _logger = logger;
    }

    public async Task<ConsultarMailLogResult> Handle(ConsultarMailLogQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var log = await _logs.GetByIdAsync(request.Id, cancellationToken);
            if (log is null)
            {
                return ConsultarMailLogResult.NotFound("Envio de e-mail nao encontrado.");
            }

            return ConsultarMailLogResult.Ok(log);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao consultar mail_log {Id}", request.Id);
            return ConsultarMailLogResult.Fail(ex.Message);
        }
    }
}

public static class EmailDestinatarios
{
    public static IReadOnlyList<string> Parse(string para) =>
        para.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(e => e.Contains('@'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static string Juntar(IEnumerable<string> destinatarios) =>
        string.Join("; ", destinatarios);
}

public static class SmtpEnvioConfigFactory
{
    public static SmtpEnvioConfig FromConta(ContaEmail conta, string senha) =>
        new(
            conta.Servidor,
            conta.PortaSmtp,
            conta.Email,
            senha,
            conta.RemetenteEfetivo,
            conta.ModoTls);
}
