using System.Net;
using System.Net.Http.Json;
using Catalog.Api.Endpoints;
using Catalog.Application.Products;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;

namespace Catalog.IntegrationTests;

[Trait("Category", "Integration")]
[Collection(CatalogApiCollection.Name)]
public sealed class ProductEndpointsTests(CatalogApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task CreateProduct_ThenGetById_ReturnsProduct_AndCachesIt()
    {
        var created = await CreateProductAsync(stock: 10);

        var product = await _client.GetFromJsonAsync<ProductResponse>($"/api/products/{created}");

        Assert.NotNull(product);
        Assert.Equal(10, product.AvailableStock);

        var cache = factory.Services.GetRequiredService<IDistributedCache>();
        Assert.NotNull(await cache.GetStringAsync($"product:{created}"));
    }

    [Fact]
    public async Task CreateProduct_WithInvalidPayload_ReturnsValidationProblem()
    {
        var response = await _client.PostAsJsonAsync("/api/products", new CreateProductRequest("", "", null, -1m, -5));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task CreateProduct_WithDuplicateSku_ReturnsConflict()
    {
        var sku = UniqueSku();
        await CreateProductAsync(sku: sku);

        var response = await _client.PostAsJsonAsync("/api/products", new CreateProductRequest(sku, "Duplicate", null, 1m, 1));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task ChangePrice_InvalidatesCachedProduct()
    {
        var id = await CreateProductAsync(price: 10m);
        await _client.GetFromJsonAsync<ProductResponse>($"/api/products/{id}");

        var response = await _client.PutAsJsonAsync($"/api/products/{id}/price", new ChangePriceRequest(15m));
        var product = await _client.GetFromJsonAsync<ProductResponse>($"/api/products/{id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(15m, product!.Price);
    }

    private async Task<Guid> CreateProductAsync(string? sku = null, decimal price = 49.90m, int stock = 5)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/products",
            new CreateProductRequest(sku ?? UniqueSku(), "Mechanical keyboard", "Hot-swappable switches", price, stock));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CreatedResponse>())!.Id;
    }

    private static string UniqueSku() => $"SKU-{Guid.NewGuid():N}"[..20];

    private sealed record CreatedResponse(Guid Id);
}
