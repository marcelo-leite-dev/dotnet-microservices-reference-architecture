using BuildingBlocks.Application;
using BuildingBlocks.Application.Cqrs;
using BuildingBlocks.Domain;
using Catalog.Application.Abstractions;
using Catalog.Domain.Products;
using FluentValidation;

namespace Catalog.Application.Products;

public sealed record GetProductByIdQuery(Guid ProductId) : IQuery<Result<ProductResponse>>;

/// <summary>Cache-aside read: Redis first, PostgreSQL on a miss, then populate the cache.</summary>
internal sealed class GetProductByIdQueryHandler(IProductQueries queries, IProductCache cache)
    : IQueryHandler<GetProductByIdQuery, Result<ProductResponse>>
{
    public async Task<Result<ProductResponse>> Handle(GetProductByIdQuery query, CancellationToken cancellationToken)
    {
        var cached = await cache.GetAsync(query.ProductId, cancellationToken);
        if (cached is not null)
        {
            return cached;
        }

        var product = await queries.GetByIdAsync(query.ProductId, cancellationToken);
        if (product is null)
        {
            return ProductErrors.NotFound(query.ProductId);
        }

        await cache.SetAsync(product, cancellationToken);
        return product;
    }
}

public sealed record GetProductsByIdsQuery(IReadOnlyCollection<Guid> ProductIds) : IQuery<IReadOnlyList<ProductResponse>>;

internal sealed class GetProductsByIdsQueryValidator : AbstractValidator<GetProductsByIdsQuery>
{
    public const int MaxIds = 100;

    public GetProductsByIdsQueryValidator()
    {
        RuleFor(q => q.ProductIds).NotEmpty().Must(ids => ids.Count <= MaxIds)
            .WithMessage($"At most {MaxIds} product ids can be requested at once.");
    }
}

internal sealed class GetProductsByIdsQueryHandler(IProductQueries queries)
    : IQueryHandler<GetProductsByIdsQuery, IReadOnlyList<ProductResponse>>
{
    public Task<IReadOnlyList<ProductResponse>> Handle(GetProductsByIdsQuery query, CancellationToken cancellationToken) =>
        queries.GetByIdsAsync(query.ProductIds.Distinct().ToArray(), cancellationToken);
}

public sealed record ListProductsQuery(int Page = 1, int PageSize = 20) : IQuery<PagedResponse<ProductResponse>>;

internal sealed class ListProductsQueryValidator : AbstractValidator<ListProductsQuery>
{
    public ListProductsQueryValidator()
    {
        RuleFor(q => q.Page).GreaterThanOrEqualTo(1);
        RuleFor(q => q.PageSize).InclusiveBetween(1, 100);
    }
}

internal sealed class ListProductsQueryHandler(IProductQueries queries)
    : IQueryHandler<ListProductsQuery, PagedResponse<ProductResponse>>
{
    public Task<PagedResponse<ProductResponse>> Handle(ListProductsQuery query, CancellationToken cancellationToken) =>
        queries.ListAsync(query.Page, query.PageSize, cancellationToken);
}
