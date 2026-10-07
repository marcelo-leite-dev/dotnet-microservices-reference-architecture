using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using NSubstitute;
using Ordering.Application.Abstractions;
using Ordering.Application.Orders;
using Ordering.Domain.Orders;

namespace Ordering.UnitTests.Application;

public sealed class PlaceOrderCommandHandlerTests
{
    private readonly ICatalogClient _catalog = Substitute.For<ICatalogClient>();
    private readonly IOrderRepository _orders = Substitute.For<IOrderRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    [Fact]
    public async Task Handle_PricesOrderWithCatalogSnapshot_AndPersistsIt()
    {
        var keyboard = new CatalogProduct(Guid.NewGuid(), "Keyboard", 100m, 10);
        GivenCatalog(keyboard);

        var result = await CreateHandler().Handle(
            new PlaceOrderCommand(Guid.NewGuid(), [new(keyboard.Id, 3)]),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        _orders.Received(1).Add(Arg.Is<Order>(o => o.Id == result.Value && o.TotalAmount == 300m && o.Status == OrderStatus.Pending));
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenAProductIsUnknown_ReturnsNotFound_AndPersistsNothing()
    {
        GivenCatalog();

        var result = await CreateHandler().Handle(
            new PlaceOrderCommand(Guid.NewGuid(), [new(Guid.NewGuid(), 1)]),
            CancellationToken.None);

        Assert.Equal("Orders.ProductsNotFound", result.Error.Code);
        _orders.DidNotReceiveWithAnyArgs().Add(default!);
    }

    [Fact]
    public async Task Handle_WhenCatalogIsUnavailable_PropagatesUnavailableError()
    {
        _catalog.GetProductsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<IReadOnlyList<CatalogProduct>>(OrderErrors.CatalogUnavailable));

        var result = await CreateHandler().Handle(
            new PlaceOrderCommand(Guid.NewGuid(), [new(Guid.NewGuid(), 1)]),
            CancellationToken.None);

        Assert.Equal(ErrorType.Unavailable, result.Error.Type);
    }

    private void GivenCatalog(params CatalogProduct[] products) =>
        _catalog.GetProductsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<CatalogProduct>>(products));

    private PlaceOrderCommandHandler CreateHandler() => new(_catalog, _orders, _unitOfWork, TimeProvider.System);
}
