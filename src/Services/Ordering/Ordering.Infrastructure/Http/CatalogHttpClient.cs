using System.ComponentModel.DataAnnotations;
using System.Net.Http.Json;
using BuildingBlocks.Domain;
using Microsoft.Extensions.Logging;
using Ordering.Application.Abstractions;
using Ordering.Domain.Orders;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace Ordering.Infrastructure.Http;

public sealed class CatalogClientOptions
{
    public const string SectionName = "Services:Catalog";

    [Required]
    [Url]
    public string BaseUrl { get; set; } = string.Empty;
}

/// <summary>
/// Typed HTTP client for the Catalog service. Retries, timeouts and the circuit breaker come from the
/// standard resilience pipeline; once they are exhausted the failure becomes a 503 for the caller.
/// </summary>
internal sealed class CatalogHttpClient(HttpClient httpClient, ILogger<CatalogHttpClient> logger) : ICatalogClient
{
    public async Task<Result<IReadOnlyList<CatalogProduct>>> GetProductsAsync(
        IReadOnlyCollection<Guid> productIds,
        CancellationToken cancellationToken)
    {
        var query = string.Join("&", productIds.Select(id => $"ids={id}"));

        try
        {
            var products = await httpClient.GetFromJsonAsync<List<CatalogProduct>>($"api/products/batch?{query}", cancellationToken);
            return Result.Success<IReadOnlyList<CatalogProduct>>(products ?? []);
        }
        catch (Exception exception) when (exception is HttpRequestException or TimeoutRejectedException or BrokenCircuitException)
        {
            logger.LogWarning(exception, "Catalog service call failed");
            return OrderErrors.CatalogUnavailable;
        }
    }
}
