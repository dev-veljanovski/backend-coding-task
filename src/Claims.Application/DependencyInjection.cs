using System.Reflection;
using Claims.Application.Abstractions.Events;
using Claims.Application.Abstractions.Messaging;
using Claims.Application.Behaviors;
using Claims.Application.Messaging;
using Claims.Domain.Covers.Pricing;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Claims.Application;

public static class DependencyInjection
{
    private static readonly Assembly ApplicationAssembly = typeof(DependencyInjection).Assembly;

    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<ISender, Sender>();
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();

        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ObservabilityBehavior<,>));
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(UnitOfWorkBehavior<,>));

        services.AddImplementationsOf(typeof(IRequestHandler<,>));
        services.AddImplementationsOf(typeof(IDomainEventHandler<>));
        services.AddValidatorsFromAssembly(ApplicationAssembly, ServiceLifetime.Singleton, includeInternalTypes: true);

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IPremiumTariff, StandardPremiumTariff>();
        services.AddSingleton<IPremiumCalculator, PremiumCalculator>();

        return services;
    }

    private static void AddImplementationsOf(this IServiceCollection services, Type openGenericInterface)
    {
        var registrations =
            from type in ApplicationAssembly.GetTypes()
            where type is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false }
            from implemented in type.GetInterfaces()
            where implemented.IsGenericType && implemented.GetGenericTypeDefinition() == openGenericInterface
            select (Service: implemented, Implementation: type);

        foreach (var (service, implementation) in registrations)
        {
            services.AddScoped(service, implementation);
        }
    }
}
