using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Pf2e.Application.Behaviours;

namespace Pf2e.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(config =>
        {
            config.RegisterServicesFromAssembly(assembly);
            config.AddOpenBehavior(typeof(LoggingBehaviour<,>));
            config.AddOpenBehavior(typeof(ValidationBehaviour<,>));
        });

        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);
        return services;
    }
}
