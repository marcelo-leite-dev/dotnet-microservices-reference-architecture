using BuildingBlocks.Domain;

namespace Catalog.Domain.Products;

/// <summary>
/// A sellable item and its available stock. The aggregate guarantees that stock never becomes negative
/// and that prices are always positive.
/// </summary>
public sealed class Product : AggregateRoot<Guid>
{
    public const int SkuMaxLength = 64;
    public const int NameMaxLength = 200;
    public const int DescriptionMaxLength = 2000;

    private Product(Guid id, string sku, string name, string description, decimal price, int availableStock)
        : base(id)
    {
        Sku = sku;
        Name = name;
        Description = description;
        Price = price;
        AvailableStock = availableStock;
    }

    // Required by EF Core.
    private Product()
    {
    }

    public string Sku { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public decimal Price { get; private set; }

    public int AvailableStock { get; private set; }

    public static string NormalizeSku(string sku) => sku.Trim().ToUpperInvariant();

    public static Result<Product> Create(string sku, string name, string? description, decimal price, int initialStock)
    {
        if (string.IsNullOrWhiteSpace(sku))
        {
            return ProductErrors.SkuRequired;
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return ProductErrors.NameRequired;
        }

        if (price <= 0)
        {
            return ProductErrors.InvalidPrice;
        }

        if (initialStock < 0)
        {
            return ProductErrors.InvalidQuantity;
        }

        return new Product(Guid.NewGuid(), NormalizeSku(sku), name.Trim(), description?.Trim() ?? string.Empty, price, initialStock);
    }

    public Result ChangePrice(decimal newPrice)
    {
        if (newPrice <= 0)
        {
            return ProductErrors.InvalidPrice;
        }

        if (newPrice == Price)
        {
            return Result.Success();
        }

        var oldPrice = Price;
        Price = newPrice;
        Raise(new ProductPriceChangedDomainEvent(Id, oldPrice, newPrice));

        return Result.Success();
    }

    public bool HasStock(int quantity) => quantity <= AvailableStock;

    public Result RemoveStock(int quantity)
    {
        if (quantity <= 0)
        {
            return ProductErrors.InvalidQuantity;
        }

        if (!HasStock(quantity))
        {
            return ProductErrors.InsufficientStock(Id, quantity, AvailableStock);
        }

        AvailableStock -= quantity;
        return Result.Success();
    }

    public Result Restock(int quantity)
    {
        if (quantity <= 0)
        {
            return ProductErrors.InvalidQuantity;
        }

        AvailableStock += quantity;
        return Result.Success();
    }
}

public sealed record ProductPriceChangedDomainEvent(Guid ProductId, decimal OldPrice, decimal NewPrice) : DomainEvent;
