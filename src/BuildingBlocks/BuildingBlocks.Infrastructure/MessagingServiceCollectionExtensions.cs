using BuildingBlocks.Infrastructure.Kafka;
using BuildingBlocks.Infrastructure.Outbox;
using BuildingBlocks.Infrastructure.Persistence;
using BuildingBlocks.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BuildingBlocks.Infrastructure;

public static class MessagingServiceCollectionExtensions
{
    /// <summary>
    /// Wires the transactional outbox, the idempotent Kafka consumer and topic provisioning for a
    /// service whose state lives in <typeparamref name="TContext"/>.
    /// </summary>
    public static IServiceCollection AddKafkaMessaging<TContext>(this IServiceCollection services, IConfiguration configuration)
        where TContext : AppDbContextBase
    {
        services.AddOptions<KafkaOptions>()
            .Bind(configuration.GetSection(KafkaOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<OutboxOptions>()
            .Bind(configuration.GetSection(OutboxOptions.SectionName));

        services.TryAddSingleton(TimeProvider.System);
        services.GetOrAddRegistry();

        services.AddSingleton<IMessageProducer, KafkaMessageProducer>();
        services.AddScoped<IOutbox, EfOutbox<TContext>>();

        // Order matters: topics must exist before the processor and the consumer start.
        services.AddHostedService<KafkaTopicInitializer>();
        services.AddHostedService<OutboxProcessor<TContext>>();
        services.AddHostedService<KafkaConsumerService<TContext>>();

        return services;
    }
}
