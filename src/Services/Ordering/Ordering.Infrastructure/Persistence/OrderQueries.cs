using Microsoft.EntityFrameworkCore;
using Ordering.Application.Abstractions;
using Ordering.Application.Orders;

namespace Ordering.Infrastructure.Persistence;

internal sealed class OrderQueries(OrderingDbContext dbContext) : IOrderQueries
{
    public Task<OrderResponse?> GetByIdAsync(Guid orderId, CancellationToken cancellationToken) =>
        dbContext.Orders.AsNoTracking()
            .Where(o => o.Id == orderId)
            .Select(o => new OrderResponse(
                o.Id,
                o.CustomerId,
                o.Status.ToString(),
                o.TotalAmount,
                o.RejectionReason,
                o.CreatedOnUtc,
                o.UpdatedOnUtc,
                o.Items.Select(i => new OrderItemResponse(i.ProductId, i.ProductName, i.UnitPrice, i.Quantity)).ToList()))
            .FirstOrDefaultAsync(cancellationToken);
}
