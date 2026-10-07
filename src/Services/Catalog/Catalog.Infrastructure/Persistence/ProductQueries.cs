using System.Linq.Expressions;
using BuildingBlocks.Application;
using Catalog.Application.Abstractions;
using Catalog.Application.Products;
using Catalog.Domain.Products;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Infrastructure.Persistence;

/// <summary>Read-side queries: no tracking, projected directly to response models.</summary>
internal sealed class ProductQueries(CatalogDbContext dbContext) : IProductQueries
{
    private static readonly Expression<Func<Product, ProductResponse>> ToResponse = p =>
        new ProductResponse(p.Id, p.Sku, p.Name, p.Description, p.Price, p.AvailableStock);

    public Task<ProductResponse?> GetByIdAsync(Guid productId, CancellationToken cancellationToken) =>
        dbContext.Products.AsNoTracking()
            .Where(p => p.Id == productId)
            .Select(ToResponse)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<ProductResponse>> GetByIdsAsync(IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken) =>
        await dbContext.Products.AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .Select(ToResponse)
            .ToListAsync(cancellationToken);

    public async Task<PagedResponse<ProductResponse>> ListAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = dbContext.Products.AsNoTracking();
        var totalCount = await query.LongCountAsync(cancellationToken);

        var items = await query
            .OrderBy(p => p.Name)
            .ThenBy(p => p.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(ToResponse)
            .ToListAsync(cancellationToken);

        return new PagedResponse<ProductResponse>(items, page, pageSize, totalCount);
    }
}
