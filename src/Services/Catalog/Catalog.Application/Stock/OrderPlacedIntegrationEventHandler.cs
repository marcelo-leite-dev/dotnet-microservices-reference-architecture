using BuildingBlocks.Application.Cqrs;
using BuildingBlocks.Messaging;
using Ordering.Contracts;

namespace Catalog.Application.Stock;

/// <summary>
/// Step 2 of the order saga (choreography): an order was placed, so try to reserve its stock.
/// Integration handlers stay thin and delegate to a command, so the regular pipeline applies.
/// </summary>
internal sealed class OrderPlacedIntegrationEventHandler(IDispatcher dispatcher) : IIntegrationEventHandler<OrderPlacedIntegrationEvent>
{
    public async Task Handle(OrderPlacedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        var items = integrationEvent.Items
            .Select(item => new ReserveStockItem(item.ProductId, item.Quantity))
            .ToList();

        // A failed reservation is a business outcome (already published), not a processing error.
        await dispatcher.Send(new ReserveStockCommand(integrationEvent.OrderId, items), cancellationToken);
    }
}
