using BuildingBlocks.Messaging;

namespace Ordering.Contracts;

public static class OrderingTopics
{
    /// <summary>Order lifecycle events, keyed by order id so that events of one order stay ordered.</summary>
    public const string Orders = "ordering.orders";
}

public sealed record OrderPlacedItem(Guid ProductId, int Quantity);

/// <summary>A customer placed an order; stock must be reserved before it can be confirmed.</summary>
public sealed record OrderPlacedIntegrationEvent(
    Guid OrderId,
    Guid CustomerId,
    IReadOnlyList<OrderPlacedItem> Items,
    decimal TotalAmount) : IntegrationEvent;

public sealed record OrderConfirmedIntegrationEvent(Guid OrderId) : IntegrationEvent;

public sealed record OrderRejectedIntegrationEvent(Guid OrderId, string Reason) : IntegrationEvent;
