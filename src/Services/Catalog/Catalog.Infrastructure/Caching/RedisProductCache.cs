using System.Diagnostics.Metrics;
using System.Text.Json;
using Catalog.Application.Abstractions;
using Catalog.Application.Products;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Catalog.Infrastructure.Caching;

public sealed class ProductCacheOptions
{
    public const string SectionName = "Caching:Products";

    public TimeSpan TimeToLive { get; set; } = TimeSpan.FromMinutes(10);
}

/// <summary>
/// Redis-backed product cache. Every operation fails open: if Redis is unavailable the request is
/// served from PostgreSQL and a warning is logged.
/// </summary>
internal sealed class RedisProductCache(
    IDistributedCache cache,
    IOptions<ProductCacheOptions> options,
    ILogger<RedisProductCache> logger) : IProductCache
{
    public const string MeterName = "Catalog.Infrastructure";

    private static readonly Meter Meter = new(MeterName);
    private static readonly Counter<long> Requests = Meter.CreateCounter<long>(
        "catalog.cache.requests",
        description: "Product cache lookups by result (hit, miss, error).");

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static string KeyFor(Guid productId) => $"product:{productId}";

    public async Task<ProductResponse?> GetAsync(Guid productId, CancellationToken cancellationToken)
    {
        try
        {
            var json = await cache.GetStringAsync(KeyFor(productId), cancellationToken);
            Requests.Add(1, new KeyValuePair<string, object?>("result", json is null ? "miss" : "hit"));
            return json is null ? null : JsonSerializer.Deserialize<ProductResponse>(json, JsonOptions);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Requests.Add(1, new KeyValuePair<string, object?>("result", "error"));
            logger.LogWarning(exception, "Product cache read failed for {ProductId}", productId);
            return null;
        }
    }

    public async Task SetAsync(ProductResponse product, CancellationToken cancellationToken)
    {
        try
        {
            await cache.SetStringAsync(
                KeyFor(product.Id),
                JsonSerializer.Serialize(product, JsonOptions),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = options.Value.TimeToLive },
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Product cache write failed for {ProductId}", product.Id);
        }
    }

    public async Task RemoveAsync(Guid productId, CancellationToken cancellationToken)
    {
        try
        {
            await cache.RemoveAsync(KeyFor(productId), cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The TTL bounds staleness if an invalidation is lost.
            logger.LogWarning(exception, "Product cache invalidation failed for {ProductId}", productId);
        }
    }
}
