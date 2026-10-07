using AutonomousAudit.Application.Data;
using AutonomousAudit.Domain;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AutonomousAudit.Application.RequestHandlers;

public record EnviarMensagemEmailCommand(
    Guid UsuarioId,
    string UsuarioToken,
    Guid ContaId,
    string Para,
    string Assunto,
    string Corpo,
    bool Html,
    string NomeOriginal,
    long TamanhoBytes,
    Stream Conteudo) : ICommand<EnviarMensagemEmailResult>;

public abstract record EnviarMensagemEmailResult
{
    public static EnviarMensagemEmailResult Created(MailLog log, ArquivoEventoPublicado nats) =>
        new EnviarMensagemEmailCreated(log, nats);

    public static EnviarMensagemEmailResult BadRequest(string message) => new EnviarMensagemEmailBadRequest(message);
    public static EnviarMensagemEmailResult NotFound(string message) => new EnviarMensagemEmailNotFound(message);
    public static EnviarMensagemEmailResult Fail(string message) => new EnviarMensagemEmailFail(message);
}

public record EnviarMensagemEmailCreated(MailLog Log, ArquivoEventoPublicado Nats) : EnviarMensagemEmailResult;
public record EnviarMensagemEmailBadRequest(string Message) : EnviarMensagemEmailResult;
public record EnviarMensagemEmailNotFound(string Message) : EnviarMensagemEmailResult;
public record EnviarMensagemEmailFail(string Message) : EnviarMensagemEmailResult;

public sealed class EnviarMensagemEmailCommandValidator : AbstractValidator<EnviarMensagemEmailCommand>
{
    public const long TamanhoMaximoAnexo = 25 * 1024 * 1024;

    public EnviarMensagemEmailCommandValidator()
    {
        RuleFor(x => x.UsuarioId).NotEmpty();
        RuleFor(x => x.ContaId).NotEmpty().WithMessage("Informe contaId da tabela mail_accounts.");
        RuleFor(x => x.Para)
            .NotEmpty().WithMessage("Informe o destinatario no campo 'para'.");
        RuleFor(x => x.Assunto)
            .NotEmpty().WithMessage("Informe o assunto.")
            .MaximumLength(500);
        RuleFor(x => x.Corpo)
            .NotEmpty().WithMessage("Informe o corpo da mensagem.");
        RuleFor(x => x.NomeOriginal)
            .NotEmpty().WithMessage("O arquivo anexo e obrigatorio.");
        RuleFor(x => x.TamanhoBytes)
            .GreaterThan(0).WithMessage("O arquivo anexo nao pode estar vazio.")
            .LessThanOrEqualTo(TamanhoMaximoAnexo)
            .WithMessage("O anexo deve ter no maximo 25 MB.");
        RuleFor(x => x.Conteudo).NotNull();
    }
}

public sealed class EnviarMensagemEmailHandler
    : IRequestHandler<EnviarMensagemEmailCommand, EnviarMensagemEmailResult>
{
    private readonly IContaEmailRepository _contas;
    private readonly IMailLogRepository _logs;
    private readonly IArquivoEventoPublisher _nats;
    private readonly IArquivoStaging _staging;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<EnviarMensagemEmailHandler> _logger;

    public EnviarMensagemEmailHandler(
        IContaEmailRepository contas,
        IMailLogRepository logs,
        IArquivoEventoPublisher nats,
        IArquivoStaging staging,
        IUnitOfWork uow,
        ILogger<EnviarMensagemEmailHandler> logger)
    {
        _contas = contas;
        _logs = logs;
        _nats = nats;
        _staging = staging;
        _uow = uow;
        _logger = logger;
    }

    public async Task<EnviarMensagemEmailResult> Handle(
        EnviarMensagemEmailCommand request,
        CancellationToken cancellationToken)
    {
        string? caminho = null;
        MailLog? log = null;
        try
        {
            var destinatarios = EmailDestinatarios.Parse(request.Para);
            if (destinatarios.Count == 0)
            {
                return EnviarMensagemEmailResult.BadRequest(
                    "Informe ao menos um e-mail valido em 'para', separado por ponto e virgula.");
            }

            var conta = await _contas.GetByIdAsync(request.ContaId, cancellationToken);
            if (conta is null)
            {
                return EnviarMensagemEmailResult.NotFound("Conta de e-mail nao encontrada.");
            }

            var nomeAnexo = Path.GetFileName(request.NomeOriginal.Trim());
            if (string.IsNullOrWhiteSpace(nomeAnexo))
            {
                return EnviarMensagemEmailResult.BadRequest("Informe o nome do arquivo anexo.");
            }

            log = new MailLog(
                conta.Id,
                request.UsuarioId,
                request.UsuarioToken,
                conta.RemetenteEfetivo,
                EmailDestinatarios.Juntar(destinatarios),
                request.Assunto.Trim(),
                nomeAnexo);

            caminho = await _staging.SalvarAsync(log.Id, request.Conteudo, cancellationToken);
            if (new FileInfo(caminho).Length == 0)
            {
                _staging.Excluir(caminho);
                return EnviarMensagemEmailResult.BadRequest("O arquivo anexo nao pode estar vazio.");
            }

            await _logs.AddAsync(log, cancellationToken);
            await _logs.SaveChangesAsync(cancellationToken);
            await _uow.CommitAsync(cancellationToken);

            var publicado = await _nats.PublicarMailAsync(
                new MailEnvioEvento(log.Id, request.Corpo, request.Html, caminho, nomeAnexo),
                cancellationToken);

            _logger.LogInformation(
                "E-mail com anexo enfileirado id={Id} conta={ContaId} anexo={Anexo} seq={Seq}",
                log.Id,
                conta.Id,
                nomeAnexo,
                publicado.Sequencia);
            return EnviarMensagemEmailResult.Created(log, publicado);
        }
        catch (Exception ex)
        {
            if (!string.IsNullOrWhiteSpace(caminho))
            {
                _staging.Excluir(caminho);
            }

            if (log is not null && log.Status == MailLogStatus.Pendente)
            {
                try
                {
                    log.MarcarFalha("Falha ao enfileirar o e-mail com anexo.", ex.Message);
                    await _logs.SaveChangesAsync(cancellationToken);
                }
                catch (Exception gravar)
                {
                    _logger.LogWarning(gravar, "Nao foi possivel marcar mail_log {Id} como falha", log.Id);
                }
            }

            _logger.LogError(ex, "Falha ao enfileirar e-mail com anexo conta={ContaId}", request.ContaId);
            return EnviarMensagemEmailResult.Fail(ex.Message);
        }
    }
}
