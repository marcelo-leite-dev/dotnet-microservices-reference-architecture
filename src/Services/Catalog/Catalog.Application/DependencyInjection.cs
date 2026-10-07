using BuildingBlocks.Application;
using BuildingBlocks.Messaging;
using Catalog.Application.Stock;
using Microsoft.Extensions.DependencyInjection;
using Ordering.Contracts;

namespace Catalog.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddCatalogApplication(this IServiceCollection services)
    {
        services.AddApplicationCore(typeof(DependencyInjection).Assembly);

        services.AddIntegrationEventHandler<OrderPlacedIntegrationEvent, OrderPlacedIntegrationEventHandler>(OrderingTopics.Orders);

        return services;
    }
}
