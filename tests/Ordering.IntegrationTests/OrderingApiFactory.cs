using System.Collections.Concurrent;
using BuildingBlocks.Domain;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Ordering.Application.Abstractions;
using Testcontainers.Kafka;
using Testcontainers.PostgreSql;
using Testing.Shared;

namespace Ordering.IntegrationTests;

/// <summary>
/// Boots the real Ordering API against PostgreSQL and Kafka containers. Only the Catalog HTTP
/// dependency is replaced: it belongs to another service and has its own tests.
/// </summary>
public sealed class OrderingApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder().WithImage("postgres:16-alpine").Build();
    private readonly KafkaContainer _kafka = new KafkaBuilder().Build();

    public FakeCatalogClient Catalog { get; } = new();

    public KafkaTestClient Kafka => new(_kafka.GetBootstrapAddress());

    public Task InitializeAsync() => Task.WhenAll(_postgres.StartAsync(), _kafka.StartAsync());

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
        await Task.WhenAll(_postgres.DisposeAsync().AsTask(), _kafka.DisposeAsync().AsTask());
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:OrderingDb", _postgres.GetConnectionString());
        builder.UseSetting("Kafka:BootstrapServers", _kafka.GetBootstrapAddress());
        builder.UseSetting("Services:Catalog:BaseUrl", "http://catalog.test");
        builder.UseSetting("Database:MigrateOnStartup", "true");
        builder.UseSetting("Outbox:PollingInterval", "00:00:00.200");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ICatalogClient>();
            services.AddSingleton<ICatalogClient>(Catalog);
        });
    }
}

public sealed class FakeCatalogClient : ICatalogClient
{
    private readonly ConcurrentDictionary<Guid, CatalogProduct> _products = new();

    public CatalogProduct AddProduct(string name, decimal price, int stock = 100)
    {
        var product = new CatalogProduct(Guid.NewGuid(), name, price, stock);
        _products[product.Id] = product;
        return product;
    }

    public Task<Result<IReadOnlyList<CatalogProduct>>> GetProductsAsync(IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken)
    {
        IReadOnlyList<CatalogProduct> found = productIds.Where(_products.ContainsKey).Select(id => _products[id]).ToList();
        return Task.FromResult(Result.Success(found));
    }
}

[CollectionDefinition(Name)]
public sealed class OrderingApiCollection : ICollectionFixture<OrderingApiFactory>
{
    public const string Name = "Ordering API";
}
