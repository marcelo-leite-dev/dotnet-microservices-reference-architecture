using BuildingBlocks.Application.Cqrs;
using BuildingBlocks.Web;
using Ordering.Application.Orders;

namespace Ordering.Api.Endpoints;

public static class OrderEndpoints
{
    public static IEndpointRouteBuilder MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var orders = app.MapGroup("/api/orders").WithTags("Orders");

        orders.MapPost("/", PlaceOrder)
            .WithName("PlaceOrder")
            .WithSummary("Places an order. The order is confirmed or rejected asynchronously once stock is reserved.")
            .Produces<PlaceOrderResponse>(StatusCodes.Status202Accepted)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        orders.MapGet("/{id:guid}", GetOrderById)
            .WithName("GetOrderById")
            .WithSummary("Gets an order and its current status (Pending, Confirmed or Rejected).")
            .Produces<OrderResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> PlaceOrder(PlaceOrderRequest request, IDispatcher dispatcher, CancellationToken cancellationToken)
    {
        var command = new PlaceOrderCommand(
            request.CustomerId,
            request.Items.Select(item => new PlaceOrderItem(item.ProductId, item.Quantity)).ToList());

        var result = await dispatcher.Send(command, cancellationToken);

        // 202 Accepted: the order exists, but its outcome is decided asynchronously by the saga.
        return result.Match(id => TypedResults.AcceptedAtRoute(new PlaceOrderResponse(id, "Pending"), "GetOrderById", new { id }));
    }

    private static async Task<IResult> GetOrderById(Guid id, IDispatcher dispatcher, CancellationToken cancellationToken)
    {
        var result = await dispatcher.Send(new GetOrderByIdQuery(id), cancellationToken);
        return result.Match(order => TypedResults.Ok(order));
    }
}

public sealed record PlaceOrderItemRequest(Guid ProductId, int Quantity);

public sealed record PlaceOrderRequest(Guid CustomerId, IReadOnlyList<PlaceOrderItemRequest> Items);

public sealed record PlaceOrderResponse(Guid OrderId, string Status);
