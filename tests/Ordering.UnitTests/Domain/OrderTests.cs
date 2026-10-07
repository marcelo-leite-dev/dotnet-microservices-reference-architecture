using Ordering.Domain.Orders;

namespace Ordering.UnitTests.Domain;

public sealed class OrderTests
{
    private static readonly DateTime Now = new(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid KeyboardId = Guid.NewGuid();
    private static readonly Guid MouseId = Guid.NewGuid();

    [Fact]
    public void Place_WithValidLines_CreatesPendingOrderWithTotal_AndRaisesOrderPlaced()
    {
        var result = Order.Place(CustomerId, [new(KeyboardId, "Keyboard", 100m, 2), new(MouseId, "Mouse", 25.5m, 1)], Now);

        Assert.True(result.IsSuccess);
        var order = result.Value;
        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.Equal(225.5m, order.TotalAmount);
        Assert.Equal(2, order.Items.Count);
        Assert.IsType<OrderPlacedDomainEvent>(Assert.Single(order.DomainEvents));
    }

    [Fact]
    public void Place_MergesLinesOfTheSameProduct()
    {
        var order = Order.Place(CustomerId, [new(KeyboardId, "Keyboard", 100m, 2), new(KeyboardId, "Keyboard", 100m, 3)], Now).Value;

        var item = Assert.Single(order.Items);
        Assert.Equal(5, item.Quantity);
        Assert.Equal(500m, order.TotalAmount);
    }

    [Fact]
    public void Place_WithoutLines_Fails()
    {
        var result = Order.Place(CustomerId, [], Now);

        Assert.Equal(OrderErrors.EmptyOrder, result.Error);
    }

    [Fact]
    public void Place_WithNonPositiveQuantity_Fails()
    {
        var result = Order.Place(CustomerId, [new(KeyboardId, "Keyboard", 100m, 0)], Now);

        Assert.Equal(OrderErrors.InvalidQuantity, result.Error);
    }

    [Fact]
    public void Confirm_PendingOrder_ConfirmsAndRaisesEvent()
    {
        var order = PlacePendingOrder();

        var result = order.Confirm(Now.AddSeconds(1));

        Assert.True(result.IsSuccess);
        Assert.Equal(OrderStatus.Confirmed, order.Status);
        Assert.IsType<OrderConfirmedDomainEvent>(Assert.Single(order.DomainEvents));
    }

    [Fact]
    public void Confirm_IsIdempotent_SoRedeliveredEventsAreHarmless()
    {
        var order = PlacePendingOrder();
        order.Confirm(Now);
        order.ClearDomainEvents();

        var result = order.Confirm(Now.AddSeconds(5));

        Assert.True(result.IsSuccess);
        Assert.Empty(order.DomainEvents);
    }

    [Fact]
    public void Confirm_RejectedOrder_Fails()
    {
        var order = PlacePendingOrder();
        order.Reject("Out of stock", Now);

        var result = order.Confirm(Now);

        Assert.Equal("Orders.InvalidStatusTransition", result.Error.Code);
        Assert.Equal(OrderStatus.Rejected, order.Status);
    }

    [Fact]
    public void Reject_PendingOrder_StoresReason()
    {
        var order = PlacePendingOrder();

        order.Reject("Out of stock", Now);

        Assert.Equal(OrderStatus.Rejected, order.Status);
        Assert.Equal("Out of stock", order.RejectionReason);
        Assert.IsType<OrderRejectedDomainEvent>(Assert.Single(order.DomainEvents));
    }

    private static Order PlacePendingOrder()
    {
        var order = Order.Place(CustomerId, [new(KeyboardId, "Keyboard", 100m, 1)], Now).Value;
        order.ClearDomainEvents();
        return order;
    }
}
