namespace Ordering.Application.Orders;

public sealed record OrderItemResponse(Guid ProductId, string ProductName, decimal UnitPrice, int Quantity);

public sealed record OrderResponse(
    Guid Id,
    Guid CustomerId,
    string Status,
    decimal TotalAmount,
    string? RejectionReason,
    DateTime CreatedOnUtc,
    DateTime UpdatedOnUtc,
    IReadOnlyList<OrderItemResponse> Items);
