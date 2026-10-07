using BuildingBlocks.Application.Cqrs;
using BuildingBlocks.Messaging;
using Catalog.Contracts;
using Microsoft.Extensions.Logging;
using Ordering.Application.Orders;

namespace Ordering.Application.IntegrationEvents;

/// <summary>Step 3 of the order saga (happy path): stock was reserved, so confirm the order.</summary>
internal sealed class StockReservedIntegrationEventHandler(IDispatcher dispatcher, ILogger<StockReservedIntegrationEventHandler> logger)
    : IIntegrationEventHandler<StockReservedIntegrationEvent>
{
    public async Task Handle(StockReservedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        var result = await dispatcher.Send(new ConfirmOrderCommand(integrationEvent.OrderId), cancellationToken);
        if (result.IsFailure)
        {
            logger.LogWarning("Order {OrderId} could not be confirmed: {Error}", integrationEvent.OrderId, result.Error.Description);
        }
    }
}

/// <summary>Step 3 of the order saga (compensation): stock could not be reserved, so reject the order.</summary>
internal sealed class StockReservationFailedIntegrationEventHandler(
    IDispatcher dispatcher,
    ILogger<StockReservationFailedIntegrationEventHandler> logger) : IIntegrationEventHandler<StockReservationFailedIntegrationEvent>
{
    public async Task Handle(StockReservationFailedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        var result = await dispatcher.Send(new RejectOrderCommand(integrationEvent.OrderId, integrationEvent.Reason), cancellationToken);
        if (result.IsFailure)
        {
            logger.LogWarning("Order {OrderId} could not be rejected: {Error}", integrationEvent.OrderId, result.Error.Description);
        }
    }
}
