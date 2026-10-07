using BuildingBlocks.Application.DomainEvents;
using BuildingBlocks.Messaging;
using Ordering.Application.Diagnostics;
using Ordering.Contracts;
using Ordering.Domain.Orders;

namespace Ordering.Application.Orders;

/// <summary>
/// Domain events are internal facts; these handlers decide which of them become public integration
/// events. Writing to the outbox here keeps the event and the state change in one transaction.
/// </summary>
internal sealed class OrderPlacedDomainEventHandler(IOutbox outbox) : IDomainEventHandler<OrderPlacedDomainEvent>
{
    public Task Handle(OrderPlacedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        var order = domainEvent.Order;

        outbox.Enqueue(
            new OrderPlacedIntegrationEvent(
                order.Id,
                order.CustomerId,
                order.Items.Select(item => new OrderPlacedItem(item.ProductId, item.Quantity)).ToList(),
                order.TotalAmount),
            OrderingTopics.Orders,
            order.Id.ToString());

        OrderingMetrics.OrdersPlaced.Add(1);
        return Task.CompletedTask;
    }
}

internal sealed class OrderConfirmedDomainEventHandler(IOutbox outbox) : IDomainEventHandler<OrderConfirmedDomainEvent>
{
    public Task Handle(OrderConfirmedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        outbox.Enqueue(new OrderConfirmedIntegrationEvent(domainEvent.OrderId), OrderingTopics.Orders, domainEvent.OrderId.ToString());

        OrderingMetrics.OrdersConfirmed.Add(1);
        return Task.CompletedTask;
    }
}

internal sealed class OrderRejectedDomainEventHandler(IOutbox outbox) : IDomainEventHandler<OrderRejectedDomainEvent>
{
    public Task Handle(OrderRejectedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        outbox.Enqueue(
            new OrderRejectedIntegrationEvent(domainEvent.OrderId, domainEvent.Reason),
            OrderingTopics.Orders,
            domainEvent.OrderId.ToString());

        OrderingMetrics.OrdersRejected.Add(1);
        return Task.CompletedTask;
    }
}
