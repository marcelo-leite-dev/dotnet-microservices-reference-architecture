using BuildingBlocks.Domain;

namespace Ordering.Domain.Orders;

public static class OrderErrors
{
    public static readonly Error CustomerRequired = Error.Validation("Orders.CustomerRequired", "The customer is required.");

    public static readonly Error EmptyOrder = Error.Validation("Orders.Empty", "An order must contain at least one item.");

    public static readonly Error InvalidQuantity = Error.Validation("Orders.InvalidQuantity", "Item quantities must be greater than zero.");

    public static readonly Error InvalidUnitPrice = Error.Validation("Orders.InvalidUnitPrice", "Item prices must be greater than zero.");

    public static readonly Error CatalogUnavailable = Error.Unavailable(
        "Orders.CatalogUnavailable",
        "The catalog could not be reached to price the order. Try again later.");

    public static Error NotFound(Guid orderId) =>
        Error.NotFound("Orders.NotFound", $"Order '{orderId}' was not found.");

    public static Error ProductsNotFound(IEnumerable<Guid> productIds) =>
        Error.NotFound("Orders.ProductsNotFound", $"Unknown product(s): {string.Join(", ", productIds)}.");

    public static Error InvalidStatusTransition(OrderStatus from, OrderStatus to) =>
        Error.Conflict("Orders.InvalidStatusTransition", $"An order cannot move from {from} to {to}.");
}
