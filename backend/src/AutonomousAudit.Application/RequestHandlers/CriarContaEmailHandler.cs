using AutonomousAudit.Application.Data;
using AutonomousAudit.Domain;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AutonomousAudit.Application.RequestHandlers;

public record CriarContaEmailCommand(
    string Email,
    string Senha,
    string Servidor,
    int PortaSmtp,
    int? PortaPop,
    string? Protocolo,
    string? ModoTls,
    string? Remetente) : ICommand<CriarContaEmailResult>;

public abstract record CriarContaEmailResult
{
    public static CriarContaEmailResult Created(ContaEmail conta) => new CriarContaEmailCreated(conta);
    public static CriarContaEmailResult BadRequest(string message) => new CriarContaEmailBadRequest(message);
    public static CriarContaEmailResult Fail(string message) => new CriarContaEmailFail(message);
}

public record CriarContaEmailCreated(ContaEmail Conta) : CriarContaEmailResult;
public record CriarContaEmailBadRequest(string Message) : CriarContaEmailResult;
public record CriarContaEmailFail(string Message) : CriarContaEmailResult;

public sealed class CriarContaEmailCommandValidator : AbstractValidator<CriarContaEmailCommand>
{
    public CriarContaEmailCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Senha).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Servidor).NotEmpty();
        RuleFor(x => x.PortaSmtp).InclusiveBetween(1, 65535);
        RuleFor(x => x.PortaPop)
            .InclusiveBetween(1, 65535)
            .When(x => x.PortaPop.HasValue);
        RuleFor(x => x.ModoTls)
            .Must(m => string.IsNullOrWhiteSpace(m)
                       || m.Equals("ssl", StringComparison.OrdinalIgnoreCase)
                       || m.Equals("starttls", StringComparison.OrdinalIgnoreCase)
                       || m.Equals("none", StringComparison.OrdinalIgnoreCase))
            .WithMessage("modoTls deve ser ssl, starttls ou none.");
        RuleFor(x => x.Remetente)
            .EmailAddress()
            .When(x => !string.IsNullOrWhiteSpace(x.Remetente))
            .WithMessage("Informe um e-mail valido em 'remetente'.");
    }
}

public sealed class CriarContaEmailHandler : IRequestHandler<CriarContaEmailCommand, CriarContaEmailResult>
{
    private readonly IContaEmailRepository _repository;
    private readonly ISegredoProtector _segredos;
    private readonly ILogger<CriarContaEmailHandler> _logger;

    public CriarContaEmailHandler(
        IContaEmailRepository repository,
        ISegredoProtector segredos,
        ILogger<CriarContaEmailHandler> logger)
    {
        _repository = repository;
        _segredos = segredos;
        _logger = logger;
    }

    public async Task<CriarContaEmailResult> Handle(CriarContaEmailCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var conta = new ContaEmail(
                request.Email,
                _segredos.Cifrar(request.Senha),
                request.Servidor,
                request.PortaSmtp,
                request.PortaPop,
                request.Protocolo ?? "SMTP",
                request.ModoTls ?? ContaEmail.ModoTlsPadrao(request.PortaSmtp),
                request.Remetente);

            await _repository.AddAsync(conta, cancellationToken);
            await _repository.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Conta SMTP cadastrada {Email} {Servidor}:{Porta}", conta.Email, conta.Servidor, conta.PortaSmtp);
            return CriarContaEmailResult.Created(conta);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao cadastrar conta SMTP {Email}", request.Email);
            return CriarContaEmailResult.Fail(ex.Message);
        }
    }
}
