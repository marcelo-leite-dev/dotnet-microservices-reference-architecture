using BuildingBlocks.Application;
using BuildingBlocks.Application.Cqrs;
using BuildingBlocks.Web;
using Catalog.Application.Products;
using Microsoft.AspNetCore.Mvc;

namespace Catalog.Api.Endpoints;

public static class ProductEndpoints
{
    public static IEndpointRouteBuilder MapProductEndpoints(this IEndpointRouteBuilder app)
    {
        var products = app.MapGroup("/api/products").WithTags("Products");

        products.MapGet("/", ListProducts)
            .WithName("ListProducts")
            .WithSummary("Lists products, ordered by name.")
            .Produces<PagedResponse<ProductResponse>>();

        products.MapGet("/{id:guid}", GetProductById)
            .WithName("GetProductById")
            .WithSummary("Gets a product (served from Redis when cached).")
            .Produces<ProductResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        products.MapGet("/batch", GetProductsByIds)
            .WithName("GetProductsByIds")
            .WithSummary("Gets several products at once (used by the Ordering service).")
            .Produces<IReadOnlyList<ProductResponse>>()
            .ProducesValidationProblem();

        products.MapPost("/", CreateProduct)
            .WithName("CreateProduct")
            .Produces(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        products.MapPut("/{id:guid}/price", ChangePrice)
            .WithName("ChangeProductPrice")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        products.MapPost("/{id:guid}/restock", Restock)
            .WithName("RestockProduct")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> ListProducts(IDispatcher dispatcher, CancellationToken cancellationToken, int page = 1, int pageSize = 20) =>
        TypedResults.Ok(await dispatcher.Send(new ListProductsQuery(page, pageSize), cancellationToken));

    private static async Task<IResult> GetProductById(Guid id, IDispatcher dispatcher, CancellationToken cancellationToken)
    {
        var result = await dispatcher.Send(new GetProductByIdQuery(id), cancellationToken);
        return result.Match(product => TypedResults.Ok(product));
    }

    private static async Task<IResult> GetProductsByIds([FromQuery] Guid[] ids, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        TypedResults.Ok(await dispatcher.Send(new GetProductsByIdsQuery(ids), cancellationToken));

    private static async Task<IResult> CreateProduct(CreateProductRequest request, IDispatcher dispatcher, CancellationToken cancellationToken)
    {
        var result = await dispatcher.Send(
            new CreateProductCommand(request.Sku, request.Name, request.Description, request.Price, request.InitialStock),
            cancellationToken);

        return result.Match(id => TypedResults.CreatedAtRoute(new { id }, "GetProductById", new { id }));
    }

    private static async Task<IResult> ChangePrice(Guid id, ChangePriceRequest request, IDispatcher dispatcher, CancellationToken cancellationToken)
    {
        var result = await dispatcher.Send(new ChangeProductPriceCommand(id, request.Price), cancellationToken);
        return result.Match(TypedResults.NoContent);
    }

    private static async Task<IResult> Restock(Guid id, RestockRequest request, IDispatcher dispatcher, CancellationToken cancellationToken)
    {
        var result = await dispatcher.Send(new RestockProductCommand(id, request.Quantity), cancellationToken);
        return result.Match(TypedResults.NoContent);
    }
}

public sealed record CreateProductRequest(string Sku, string Name, string? Description, decimal Price, int InitialStock);

public sealed record ChangePriceRequest(decimal Price);

public sealed record RestockRequest(int Quantity);
