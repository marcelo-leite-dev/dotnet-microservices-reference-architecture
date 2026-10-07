using BuildingBlocks.Domain;

namespace Ordering.Application.Abstractions;

/// <summary>Price and availability snapshot of a product, as returned by the Catalog service.</summary>
public sealed record CatalogProduct(Guid Id, string Name, decimal Price, int AvailableStock);

/// <summary>
/// Synchronous query to the Catalog service, used only to price an order at placement time.
/// Stock is not reserved here: that happens asynchronously through events.
/// </summary>
public interface ICatalogClient
{
    Task<Result<IReadOnlyList<CatalogProduct>>> GetProductsAsync(IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken);
}
