using BuildingBlocks.Domain;
using Catalog.Domain.Products;

namespace Catalog.UnitTests.Domain;

public sealed class ProductTests
{
    [Fact]
    public void Create_WithValidData_NormalizesSku()
    {
        var result = Product.Create("  kb-01 ", "Keyboard", null, 99.90m, 10);

        Assert.True(result.IsSuccess);
        Assert.Equal("KB-01", result.Value.Sku);
        Assert.Equal(string.Empty, result.Value.Description);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_WithNonPositivePrice_Fails(decimal price)
    {
        var result = Product.Create("KB-01", "Keyboard", null, price, 10);

        Assert.Equal(ProductErrors.InvalidPrice, result.Error);
    }

    [Fact]
    public void RemoveStock_BeyondAvailable_FailsAndKeepsStock()
    {
        var product = Product.Create("KB-01", "Keyboard", null, 10m, 3).Value;

        var result = product.RemoveStock(4);

        Assert.True(result.IsFailure);
        Assert.Equal("Products.InsufficientStock", result.Error.Code);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Equal(3, product.AvailableStock);
    }

    [Fact]
    public void RemoveStock_WithinAvailable_DecrementsStock()
    {
        var product = Product.Create("KB-01", "Keyboard", null, 10m, 3).Value;

        var result = product.RemoveStock(3);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, product.AvailableStock);
    }

    [Fact]
    public void ChangePrice_ToNewValue_RaisesDomainEvent()
    {
        var product = Product.Create("KB-01", "Keyboard", null, 10m, 3).Value;

        product.ChangePrice(12.5m);

        var domainEvent = Assert.IsType<ProductPriceChangedDomainEvent>(Assert.Single(product.DomainEvents));
        Assert.Equal(10m, domainEvent.OldPrice);
        Assert.Equal(12.5m, domainEvent.NewPrice);
    }

    [Fact]
    public void ChangePrice_ToSameValue_IsANoOp()
    {
        var product = Product.Create("KB-01", "Keyboard", null, 10m, 3).Value;

        var result = product.ChangePrice(10m);

        Assert.True(result.IsSuccess);
        Assert.Empty(product.DomainEvents);
    }
}
