using System.Reflection;
using BuildingBlocks.Application.Behaviors;
using BuildingBlocks.Application.Cqrs;
using BuildingBlocks.Application.DomainEvents;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BuildingBlocks.Application;

public static class ApplicationServiceCollectionExtensions
{
    /// <summary>
    /// Registers the dispatcher, the default pipeline (instrumentation, then validation) and every
    /// request handler, domain event handler and validator found in <paramref name="assembly"/>.
    /// </summary>
    public static IServiceCollection AddApplicationCore(this IServiceCollection services, Assembly assembly)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddScoped<IDispatcher, Dispatcher>();
        services.TryAddScoped<IDomainEventDispatcher, DomainEventDispatcher>();

        services.TryAddEnumerable(ServiceDescriptor.Scoped(typeof(IPipelineBehavior<,>), typeof(InstrumentationBehavior<,>)));
        services.TryAddEnumerable(ServiceDescriptor.Scoped(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>)));

        RegisterClosedImplementations(services, assembly, typeof(IRequestHandler<,>));
        RegisterClosedImplementations(services, assembly, typeof(IDomainEventHandler<>));

        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

        return services;
    }

    private static void RegisterClosedImplementations(IServiceCollection services, Assembly assembly, Type openGenericInterface)
    {
        var implementations = assembly.GetTypes()
            .Where(type => type is { IsAbstract: false, IsInterface: false, IsGenericTypeDefinition: false });

        foreach (var implementation in implementations)
        {
            var closedInterfaces = implementation.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == openGenericInterface);

            foreach (var closedInterface in closedInterfaces)
            {
                services.AddScoped(closedInterface, implementation);
            }
        }
    }
}
