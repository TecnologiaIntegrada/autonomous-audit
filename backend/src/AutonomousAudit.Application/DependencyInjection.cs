using AutonomousAudit.Application.RequestHandlers;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace AutonomousAudit.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(ReceberArquivoHandler).Assembly));
        services.AddValidatorsFromAssemblyContaining<ReceberArquivoCommandValidator>();
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(UnitOfWorkBehavior<,>));
        return services;
    }
}
