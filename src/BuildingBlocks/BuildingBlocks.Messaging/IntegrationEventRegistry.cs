using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.Messaging;

public sealed record IntegrationEventRegistration(
    Type EventType,
    string Topic,
    Func<IServiceProvider, IntegrationEvent, CancellationToken, Task> Handle);

/// <summary>
/// Maps wire event names to CLR types and handlers, and knows which topics the service must
/// subscribe to. Populated at startup by <see cref="IntegrationEventServiceCollectionExtensions.AddIntegrationEventHandler{TEvent,THandler}"/>.
/// </summary>
public sealed class IntegrationEventRegistry
{
    private readonly Dictionary<string, IntegrationEventRegistration> _registrations = new(StringComparer.Ordinal);

    public IReadOnlyCollection<string> Topics => _registrations.Values.Select(r => r.Topic).Distinct().ToArray();

    public bool TryGet(string eventName, [NotNullWhen(true)] out IntegrationEventRegistration? registration) =>
        _registrations.TryGetValue(eventName, out registration);

    internal void Register<TEvent>(string topic)
        where TEvent : IntegrationEvent
    {
        _registrations[IntegrationEventNames.For<TEvent>()] = new IntegrationEventRegistration(
            typeof(TEvent),
            topic,
            static (serviceProvider, integrationEvent, cancellationToken) => serviceProvider
                .GetRequiredService<IIntegrationEventHandler<TEvent>>()
                .Handle((TEvent)integrationEvent, cancellationToken));
    }
}

public static class IntegrationEventServiceCollectionExtensions
{
    /// <summary>Subscribes the service to <paramref name="topic"/> and routes <typeparamref name="TEvent"/> to <typeparamref name="THandler"/>.</summary>
    public static IServiceCollection AddIntegrationEventHandler<TEvent, THandler>(this IServiceCollection services, string topic)
        where TEvent : IntegrationEvent
        where THandler : class, IIntegrationEventHandler<TEvent>
    {
        GetOrAddRegistry(services).Register<TEvent>(topic);
        services.AddScoped<IIntegrationEventHandler<TEvent>, THandler>();
        return services;
    }

    public static IntegrationEventRegistry GetOrAddRegistry(this IServiceCollection services)
    {
        var existing = services.FirstOrDefault(d => d.ServiceType == typeof(IntegrationEventRegistry));
        if (existing?.ImplementationInstance is IntegrationEventRegistry registry)
        {
            return registry;
        }

        registry = new IntegrationEventRegistry();
        services.AddSingleton(registry);
        return registry;
    }
}
