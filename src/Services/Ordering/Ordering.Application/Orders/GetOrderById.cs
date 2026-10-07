using BuildingBlocks.Application.Cqrs;
using BuildingBlocks.Domain;
using Ordering.Application.Abstractions;
using Ordering.Domain.Orders;

namespace Ordering.Application.Orders;

public sealed record GetOrderByIdQuery(Guid OrderId) : IQuery<Result<OrderResponse>>;

internal sealed class GetOrderByIdQueryHandler(IOrderQueries queries) : IQueryHandler<GetOrderByIdQuery, Result<OrderResponse>>
{
    public async Task<Result<OrderResponse>> Handle(GetOrderByIdQuery query, CancellationToken cancellationToken)
    {
        var order = await queries.GetByIdAsync(query.OrderId, cancellationToken);
        return order is null ? OrderErrors.NotFound(query.OrderId) : order;
    }
}
