using BuildingBlocks.Application;
using BuildingBlocks.Messaging;
using Catalog.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Ordering.Application.IntegrationEvents;

namespace Ordering.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddOrderingApplication(this IServiceCollection services)
    {
        services.AddApplicationCore(typeof(DependencyInjection).Assembly);

        services.AddIntegrationEventHandler<StockReservedIntegrationEvent, StockReservedIntegrationEventHandler>(CatalogTopics.StockReservations);
        services.AddIntegrationEventHandler<StockReservationFailedIntegrationEvent, StockReservationFailedIntegrationEventHandler>(CatalogTopics.StockReservations);

        return services;
    }
}
