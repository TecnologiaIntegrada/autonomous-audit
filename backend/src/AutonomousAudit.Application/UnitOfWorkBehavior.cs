using AutonomousAudit.Application.Data;
using MediatR;

namespace AutonomousAudit.Application;

public sealed class UnitOfWorkBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IUnitOfWork _uow;

    public UnitOfWorkBehavior(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (request is not ICommand)
        {
            return await next();
        }

        await _uow.BeginAsync(cancellationToken);
        try
        {
            var response = await next();
            await _uow.CommitAsync(cancellationToken);
            return response;
        }
        catch
        {
            await _uow.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
