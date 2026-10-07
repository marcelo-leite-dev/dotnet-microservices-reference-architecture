using BuildingBlocks.Messaging;

namespace Catalog.Contracts;

public static class CatalogTopics
{
    /// <summary>Product lifecycle events, keyed by product id.</summary>
    public const string Products = "catalog.products";

    /// <summary>Outcome of stock reservations requested by orders, keyed by order id.</summary>
    public const string StockReservations = "catalog.stock-reservations";
}

/// <summary>All items of the order were reserved; stock was decremented.</summary>
public sealed record StockReservedIntegrationEvent(Guid OrderId) : IntegrationEvent;

/// <summary>The order could not be fulfilled; no stock was changed.</summary>
public sealed record StockReservationFailedIntegrationEvent(Guid OrderId, string Reason) : IntegrationEvent;

public sealed record ProductPriceChangedIntegrationEvent(Guid ProductId, decimal OldPrice, decimal NewPrice) : IntegrationEvent;
