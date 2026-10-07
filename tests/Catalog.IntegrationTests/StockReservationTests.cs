using System.Net.Http.Json;
using Catalog.Api.Endpoints;
using Catalog.Application.Products;
using Catalog.Contracts;
using Ordering.Contracts;
using Testing.Shared;

namespace Catalog.IntegrationTests;

/// <summary>Exercises the Catalog's side of the order saga end-to-end through Kafka.</summary>
[Trait("Category", "Integration")]
[Collection(CatalogApiCollection.Name)]
public sealed class StockReservationTests(CatalogApiFactory factory)
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task OrderPlaced_WithEnoughStock_ReservesStock_AndPublishesStockReserved()
    {
        var productId = await CreateProductAsync(stock: 5);
        var orderId = Guid.NewGuid();

        await factory.Kafka.PublishAsync(
            OrderingTopics.Orders,
            orderId.ToString(),
            new OrderPlacedIntegrationEvent(orderId, Guid.NewGuid(), [new OrderPlacedItem(productId, 2)], 99.80m));

        var reserved = await factory.Kafka.WaitForEventAsync<StockReservedIntegrationEvent>(
            CatalogTopics.StockReservations, e => e.OrderId == orderId, Timeout);
        var product = await _client.GetFromJsonAsync<ProductResponse>($"/api/products/{productId}");

        Assert.NotNull(reserved);
        Assert.Equal(3, product!.AvailableStock);
    }

    [Fact]
    public async Task OrderPlaced_WithInsufficientStock_PublishesReservationFailed_AndKeepsStock()
    {
        var productId = await CreateProductAsync(stock: 1);
        var orderId = Guid.NewGuid();

        await factory.Kafka.PublishAsync(
            OrderingTopics.Orders,
            orderId.ToString(),
            new OrderPlacedIntegrationEvent(orderId, Guid.NewGuid(), [new OrderPlacedItem(productId, 2)], 99.80m));

        var failed = await factory.Kafka.WaitForEventAsync<StockReservationFailedIntegrationEvent>(
            CatalogTopics.StockReservations, e => e.OrderId == orderId, Timeout);
        var product = await _client.GetFromJsonAsync<ProductResponse>($"/api/products/{productId}");

        Assert.NotNull(failed);
        Assert.Equal(1, product!.AvailableStock);
    }

    [Fact]
    public async Task DuplicateOrderPlacedEvent_IsProcessedOnlyOnce()
    {
        var productId = await CreateProductAsync(stock: 10);
        var orderId = Guid.NewGuid();
        var integrationEvent = new OrderPlacedIntegrationEvent(orderId, Guid.NewGuid(), [new OrderPlacedItem(productId, 4)], 10m);

        // Same message id twice: simulates a broker redelivery.
        await factory.Kafka.PublishAsync(OrderingTopics.Orders, orderId.ToString(), integrationEvent);
        await factory.Kafka.PublishAsync(OrderingTopics.Orders, orderId.ToString(), integrationEvent);

        await factory.Kafka.WaitForEventAsync<StockReservedIntegrationEvent>(CatalogTopics.StockReservations, e => e.OrderId == orderId, Timeout);
        var product = await Eventually.MatchesAsync(
            () => _client.GetFromJsonAsync<ProductResponse>($"/api/products/{productId}"),
            p => p!.AvailableStock == 6,
            TimeSpan.FromSeconds(5));

        Assert.Equal(6, product!.AvailableStock);
    }

    private async Task<Guid> CreateProductAsync(int stock)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/products",
            new CreateProductRequest($"SKU-{Guid.NewGuid():N}"[..20], "Wireless mouse", null, 49.90m, stock));
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<CreatedResponse>())!.Id;
    }

    private sealed record CreatedResponse(Guid Id);
}
