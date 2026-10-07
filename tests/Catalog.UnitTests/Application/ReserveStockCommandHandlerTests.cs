using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Messaging;
using Catalog.Application.Abstractions;
using Catalog.Application.Stock;
using Catalog.Contracts;
using Catalog.Domain.Products;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Catalog.UnitTests.Application;

public sealed class ReserveStockCommandHandlerTests
{
    private readonly IProductRepository _products = Substitute.For<IProductRepository>();
    private readonly IProductCache _cache = Substitute.For<IProductCache>();
    private readonly IOutbox _outbox = Substitute.For<IOutbox>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    [Fact]
    public async Task Handle_WhenAllItemsAreAvailable_ReservesStockAndPublishesStockReserved()
    {
        var keyboard = Product.Create("KB-01", "Keyboard", null, 100m, 5).Value;
        var mouse = Product.Create("MS-01", "Mouse", null, 30m, 2).Value;
        GivenProducts(keyboard, mouse);
        var orderId = Guid.NewGuid();

        var result = await CreateHandler().Handle(
            new ReserveStockCommand(orderId, [new(keyboard.Id, 2), new(mouse.Id, 1), new(keyboard.Id, 1)]),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, keyboard.AvailableStock);
        Assert.Equal(1, mouse.AvailableStock);
        _outbox.Received(1).Enqueue(
            Arg.Is<StockReservedIntegrationEvent>(e => e.OrderId == orderId),
            CatalogTopics.StockReservations,
            orderId.ToString());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _cache.Received(1).RemoveAsync(keyboard.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenAnyItemIsShort_ChangesNothingAndPublishesReservationFailed()
    {
        var keyboard = Product.Create("KB-01", "Keyboard", null, 100m, 5).Value;
        var mouse = Product.Create("MS-01", "Mouse", null, 30m, 0).Value;
        GivenProducts(keyboard, mouse);
        var orderId = Guid.NewGuid();

        var result = await CreateHandler().Handle(
            new ReserveStockCommand(orderId, [new(keyboard.Id, 2), new(mouse.Id, 1)]),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(5, keyboard.AvailableStock);
        _outbox.Received(1).Enqueue(
            Arg.Is<StockReservationFailedIntegrationEvent>(e => e.OrderId == orderId && e.Reason.Contains(mouse.Id.ToString())),
            CatalogTopics.StockReservations,
            orderId.ToString());
        _outbox.DidNotReceive().Enqueue(Arg.Any<StockReservedIntegrationEvent>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task Handle_WhenProductIsUnknown_PublishesReservationFailed()
    {
        GivenProducts();
        var orderId = Guid.NewGuid();

        var result = await CreateHandler().Handle(
            new ReserveStockCommand(orderId, [new(Guid.NewGuid(), 1)]),
            CancellationToken.None);

        Assert.Equal("Products.NotFound", result.Error.Code);
        _outbox.Received(1).Enqueue(Arg.Any<StockReservationFailedIntegrationEvent>(), CatalogTopics.StockReservations, orderId.ToString());
    }

    private void GivenProducts(params Product[] products) =>
        _products.GetByIdsAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(products);

    private ReserveStockCommandHandler CreateHandler() =>
        new(_products, _cache, _outbox, _unitOfWork, NullLogger<ReserveStockCommandHandler>.Instance);
}
