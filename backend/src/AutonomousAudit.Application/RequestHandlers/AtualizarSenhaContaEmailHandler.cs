using AutonomousAudit.Application.Data;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AutonomousAudit.Application.RequestHandlers;

public record AtualizarSenhaContaEmailCommand(Guid Id, string Senha) : ICommand<AtualizarSenhaContaEmailResult>;

public abstract record AtualizarSenhaContaEmailResult
{
    public static AtualizarSenhaContaEmailResult Ok(Domain.ContaEmail conta) => new AtualizarSenhaContaEmailOk(conta);
    public static AtualizarSenhaContaEmailResult NotFound(string message) => new AtualizarSenhaContaEmailNotFound(message);
    public static AtualizarSenhaContaEmailResult BadRequest(string message) => new AtualizarSenhaContaEmailBadRequest(message);
    public static AtualizarSenhaContaEmailResult Fail(string message) => new AtualizarSenhaContaEmailFail(message);
}

public record AtualizarSenhaContaEmailOk(Domain.ContaEmail Conta) : AtualizarSenhaContaEmailResult;
public record AtualizarSenhaContaEmailNotFound(string Message) : AtualizarSenhaContaEmailResult;
public record AtualizarSenhaContaEmailBadRequest(string Message) : AtualizarSenhaContaEmailResult;
public record AtualizarSenhaContaEmailFail(string Message) : AtualizarSenhaContaEmailResult;

public sealed class AtualizarSenhaContaEmailCommandValidator : AbstractValidator<AtualizarSenhaContaEmailCommand>
{
    public AtualizarSenhaContaEmailCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Senha).NotEmpty().MaximumLength(200);
    }
}

public sealed class AtualizarSenhaContaEmailHandler : IRequestHandler<AtualizarSenhaContaEmailCommand, AtualizarSenhaContaEmailResult>
{
    private readonly IContaEmailRepository _repository;
    private readonly ISegredoProtector _segredos;
    private readonly ILogger<AtualizarSenhaContaEmailHandler> _logger;

    public AtualizarSenhaContaEmailHandler(
        IContaEmailRepository repository,
        ISegredoProtector segredos,
        ILogger<AtualizarSenhaContaEmailHandler> logger)
    {
        _repository = repository;
        _segredos = segredos;
        _logger = logger;
    }

    public async Task<AtualizarSenhaContaEmailResult> Handle(
        AtualizarSenhaContaEmailCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            var conta = await _repository.GetByIdAsync(request.Id, cancellationToken);
            if (conta is null)
            {
                _logger.LogWarning("Conta SMTP nao encontrada para atualizar senha {ContaId}", request.Id);
                return AtualizarSenhaContaEmailResult.NotFound("Conta de e-mail nao encontrada.");
            }

            conta.DefinirSenha(_segredos.Cifrar(request.Senha));
            await _repository.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Senha da conta SMTP atualizada {ContaId} {Email}", conta.Id, conta.Email);
            return AtualizarSenhaContaEmailResult.Ok(conta);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Senha invalida ao atualizar conta SMTP {ContaId}", request.Id);
            return AtualizarSenhaContaEmailResult.BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao atualizar senha da conta SMTP {ContaId}", request.Id);
            return AtualizarSenhaContaEmailResult.Fail(ex.Message);
        }
    }
}
