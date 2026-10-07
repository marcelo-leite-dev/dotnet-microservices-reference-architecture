namespace Catalog.Application.Products;

public sealed record ProductResponse(
    Guid Id,
    string Sku,
    string Name,
    string Description,
    decimal Price,
    int AvailableStock);
