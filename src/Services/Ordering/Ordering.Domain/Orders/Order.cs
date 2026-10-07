using BuildingBlocks.Domain;

namespace Ordering.Domain.Orders;

public enum OrderStatus
{
    /// <summary>Placed; waiting for the Catalog to reserve stock.</summary>
    Pending = 0,

    /// <summary>Stock reserved; the order will be fulfilled.</summary>
    Confirmed = 1,

    /// <summary>Stock could not be reserved.</summary>
    Rejected = 2,
}

/// <summary>Input line used to place an order, with the price snapshot taken from the Catalog.</summary>
public sealed record OrderLine(Guid ProductId, string ProductName, decimal UnitPrice, int Quantity);

/// <summary>
/// A customer's order. Its lifecycle is a small state machine driven by the stock reservation outcome:
/// Pending → Confirmed | Rejected. Transitions are idempotent so redelivered events are harmless.
/// </summary>
public sealed class Order : AggregateRoot<Guid>
{
    private readonly List<OrderItem> _items = [];

    private Order(Guid id, Guid customerId, DateTime createdOnUtc)
        : base(id)
    {
        CustomerId = customerId;
        Status = OrderStatus.Pending;
        CreatedOnUtc = createdOnUtc;
        UpdatedOnUtc = createdOnUtc;
    }

    // Required by EF Core.
    private Order()
    {
    }

    public Guid CustomerId { get; private set; }

    public OrderStatus Status { get; private set; }

    public decimal TotalAmount { get; private set; }

    public string? RejectionReason { get; private set; }

    public DateTime CreatedOnUtc { get; private set; }

    public DateTime UpdatedOnUtc { get; private set; }

    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();

    public static Result<Order> Place(Guid customerId, IReadOnlyCollection<OrderLine> lines, DateTime nowUtc)
    {
        if (customerId == Guid.Empty)
        {
            return OrderErrors.CustomerRequired;
        }

        if (lines.Count == 0)
        {
            return OrderErrors.EmptyOrder;
        }

        if (lines.Any(line => line.Quantity <= 0))
        {
            return OrderErrors.InvalidQuantity;
        }

        if (lines.Any(line => line.UnitPrice <= 0))
        {
            return OrderErrors.InvalidUnitPrice;
        }

        var order = new Order(Guid.NewGuid(), customerId, nowUtc);

        // Several lines of the same product are merged into a single item.
        foreach (var productLines in lines.GroupBy(line => line.ProductId))
        {
            var first = productLines.First();
            order._items.Add(new OrderItem(Guid.NewGuid(), first.ProductId, first.ProductName, first.UnitPrice, productLines.Sum(l => l.Quantity)));
        }

        order.TotalAmount = order._items.Sum(item => item.LineTotal);
        order.Raise(new OrderPlacedDomainEvent(order));

        return order;
    }

    public Result Confirm(DateTime nowUtc)
    {
        if (Status == OrderStatus.Confirmed)
        {
            return Result.Success();
        }

        if (Status != OrderStatus.Pending)
        {
            return OrderErrors.InvalidStatusTransition(Status, OrderStatus.Confirmed);
        }

        Status = OrderStatus.Confirmed;
        UpdatedOnUtc = nowUtc;
        Raise(new OrderConfirmedDomainEvent(Id));

        return Result.Success();
    }

    public Result Reject(string reason, DateTime nowUtc)
    {
        if (Status == OrderStatus.Rejected)
        {
            return Result.Success();
        }

        if (Status != OrderStatus.Pending)
        {
            return OrderErrors.InvalidStatusTransition(Status, OrderStatus.Rejected);
        }

        Status = OrderStatus.Rejected;
        RejectionReason = reason;
        UpdatedOnUtc = nowUtc;
        Raise(new OrderRejectedDomainEvent(Id, reason));

        return Result.Success();
    }
}

public sealed class OrderItem : Entity<Guid>
{
    internal OrderItem(Guid id, Guid productId, string productName, decimal unitPrice, int quantity)
        : base(id)
    {
        ProductId = productId;
        ProductName = productName;
        UnitPrice = unitPrice;
        Quantity = quantity;
    }

    // Required by EF Core.
    private OrderItem()
    {
    }

    public Guid ProductId { get; private set; }

    public string ProductName { get; private set; } = string.Empty;

    public decimal UnitPrice { get; private set; }

    public int Quantity { get; private set; }

    public decimal LineTotal => UnitPrice * Quantity;
}

public sealed record OrderPlacedDomainEvent(Order Order) : DomainEvent;

public sealed record OrderConfirmedDomainEvent(Guid OrderId) : DomainEvent;

public sealed record OrderRejectedDomainEvent(Guid OrderId, string Reason) : DomainEvent;
