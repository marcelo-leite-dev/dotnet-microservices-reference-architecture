using BuildingBlocks.Domain;

namespace Catalog.Domain.Products;

public static class ProductErrors
{
    public static readonly Error SkuRequired = Error.Validation("Products.SkuRequired", "The SKU is required.");

    public static readonly Error NameRequired = Error.Validation("Products.NameRequired", "The name is required.");

    public static readonly Error InvalidPrice = Error.Validation("Products.InvalidPrice", "The price must be greater than zero.");

    public static readonly Error InvalidQuantity = Error.Validation("Products.InvalidQuantity", "The quantity must be greater than zero.");

    public static Error NotFound(Guid productId) =>
        Error.NotFound("Products.NotFound", $"Product '{productId}' was not found.");

    public static Error DuplicateSku(string sku) =>
        Error.Conflict("Products.DuplicateSku", $"A product with SKU '{sku}' already exists.");

    public static Error InsufficientStock(Guid productId, int requested, int available) =>
        Error.Conflict(
            "Products.InsufficientStock",
            $"Product '{productId}' has {available} unit(s) in stock; {requested} requested.");
}
