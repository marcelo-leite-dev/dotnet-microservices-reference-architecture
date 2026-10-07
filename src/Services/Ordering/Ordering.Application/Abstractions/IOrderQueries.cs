using Ordering.Application.Orders;

namespace Ordering.Application.Abstractions;

public interface IOrderQueries
{
    Task<OrderResponse?> GetByIdAsync(Guid orderId, CancellationToken cancellationToken);
}
