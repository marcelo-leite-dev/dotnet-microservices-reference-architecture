using Catalog.Application.Abstractions;
using Catalog.Application.Products;
using NSubstitute;

namespace Catalog.UnitTests.Application;

public sealed class GetProductByIdQueryHandlerTests
{
    private readonly IProductQueries _queries = Substitute.For<IProductQueries>();
    private readonly IProductCache _cache = Substitute.For<IProductCache>();

    [Fact]
    public async Task Handle_OnCacheHit_DoesNotQueryTheDatabase()
    {
        var product = new ProductResponse(Guid.NewGuid(), "KB-01", "Keyboard", "", 100m, 5);
        _cache.GetAsync(product.Id, Arg.Any<CancellationToken>()).Returns(product);

        var result = await new GetProductByIdQueryHandler(_queries, _cache).Handle(new GetProductByIdQuery(product.Id), CancellationToken.None);

        Assert.Equal(product, result.Value);
        await _queries.DidNotReceiveWithAnyArgs().GetByIdAsync(default, default);
    }

    [Fact]
    public async Task Handle_OnCacheMiss_LoadsFromDatabaseAndPopulatesCache()
    {
        var product = new ProductResponse(Guid.NewGuid(), "KB-01", "Keyboard", "", 100m, 5);
        _queries.GetByIdAsync(product.Id, Arg.Any<CancellationToken>()).Returns(product);

        var result = await new GetProductByIdQueryHandler(_queries, _cache).Handle(new GetProductByIdQuery(product.Id), CancellationToken.None);

        Assert.Equal(product, result.Value);
        await _cache.Received(1).SetAsync(product, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenProductDoesNotExist_ReturnsNotFound()
    {
        var result = await new GetProductByIdQueryHandler(_queries, _cache).Handle(new GetProductByIdQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.Equal("Products.NotFound", result.Error.Code);
        await _cache.DidNotReceiveWithAnyArgs().SetAsync(default!, default);
    }
}
