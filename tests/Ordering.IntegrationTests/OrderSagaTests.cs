using System.Net;
using System.Net.Http.Json;
using Catalog.Contracts;
using Ordering.Api.Endpoints;
using Ordering.Application.Orders;
using Ordering.Contracts;
using Testing.Shared;

namespace Ordering.IntegrationTests;

/// <summary>Exercises the Ordering side of the saga: HTTP in, outbox to Kafka, events back in.</summary>
[Trait("Category", "Integration")]
[Collection(OrderingApiCollection.Name)]
public sealed class OrderSagaTests(OrderingApiFactory factory)
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task PlaceOrder_ReturnsAccepted_PersistsPendingOrder_AndPublishesOrderPlaced()
    {
        var keyboard = factory.Catalog.AddProduct("Keyboard", 100m);

        var response = await _client.PostAsJsonAsync("/api/orders", new PlaceOrderRequest(Guid.NewGuid(), [new(keyboard.Id, 2)]));
        var placed = await response.Content.ReadFromJsonAsync<PlaceOrderResponse>();

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var order = await _client.GetFromJsonAsync<OrderResponse>($"/api/orders/{placed!.OrderId}");
        Assert.Equal("Pending", order!.Status);
        Assert.Equal(200m, order.TotalAmount);

        var published = await factory.Kafka.WaitForEventAsync<OrderPlacedIntegrationEvent>(
            OrderingTopics.Orders, e => e.OrderId == placed.OrderId, Timeout);
        Assert.NotNull(published);
        Assert.Equal(2, Assert.Single(published.Items).Quantity);
    }

    [Fact]
    public async Task PlaceOrder_WithUnknownProduct_ReturnsNotFound()
    {
        var response = await _client.PostAsJsonAsync("/api/orders", new PlaceOrderRequest(Guid.NewGuid(), [new(Guid.NewGuid(), 1)]));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PlaceOrder_WithInvalidPayload_ReturnsValidationProblem()
    {
        var response = await _client.PostAsJsonAsync("/api/orders", new PlaceOrderRequest(Guid.Empty, []));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task StockReserved_ConfirmsOrder()
    {
        var orderId = await PlaceOrderAsync();

        await factory.Kafka.PublishAsync(CatalogTopics.StockReservations, orderId.ToString(), new StockReservedIntegrationEvent(orderId));

        var order = await WaitForStatusAsync(orderId, "Confirmed");
        Assert.Equal("Confirmed", order.Status);
    }

    [Fact]
    public async Task StockReservationFailed_RejectsOrder_WithReason()
    {
        var orderId = await PlaceOrderAsync();

        await factory.Kafka.PublishAsync(
            CatalogTopics.StockReservations,
            orderId.ToString(),
            new StockReservationFailedIntegrationEvent(orderId, "Out of stock"));

        var order = await WaitForStatusAsync(orderId, "Rejected");
        Assert.Equal("Rejected", order.Status);
        Assert.Equal("Out of stock", order.RejectionReason);
    }

    private async Task<Guid> PlaceOrderAsync()
    {
        var mouse = factory.Catalog.AddProduct("Mouse", 30m);
        var response = await _client.PostAsJsonAsync("/api/orders", new PlaceOrderRequest(Guid.NewGuid(), [new(mouse.Id, 1)]));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PlaceOrderResponse>())!.OrderId;
    }

    private Task<OrderResponse> WaitForStatusAsync(Guid orderId, string status) =>
        Eventually.MatchesAsync(
            async () => (await _client.GetFromJsonAsync<OrderResponse>($"/api/orders/{orderId}"))!,
            order => order.Status == status,
            Timeout);
}
