using BuildingBlocks.Application;
using Catalog.Application.Products;

namespace Catalog.Application.Abstractions;

/// <summary>
/// Read side of CQRS: queries bypass the aggregate and project straight into response models.
/// </summary>
public interface IProductQueries
{
    Task<ProductResponse?> GetByIdAsync(Guid productId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ProductResponse>> GetByIdsAsync(IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken);

    Task<PagedResponse<ProductResponse>> ListAsync(int page, int pageSize, CancellationToken cancellationToken);
}

/// <summary>
/// Cache-aside store for product reads. Implementations must fail open: a cache outage degrades
/// latency, never availability.
/// </summary>
public interface IProductCache
{
    Task<ProductResponse?> GetAsync(Guid productId, CancellationToken cancellationToken);

    Task SetAsync(ProductResponse product, CancellationToken cancellationToken);

    Task RemoveAsync(Guid productId, CancellationToken cancellationToken);
}
