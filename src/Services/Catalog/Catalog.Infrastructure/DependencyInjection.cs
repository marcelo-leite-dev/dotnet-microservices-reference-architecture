using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Infrastructure;
using Catalog.Application.Abstractions;
using Catalog.Domain.Products;
using Catalog.Infrastructure.Caching;
using Catalog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Trace;
using StackExchange.Redis;

namespace Catalog.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddCatalogInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Connection strings are resolved lazily so that hosts (and tests) can override configuration late.
        services.AddDbContext<CatalogDbContext>((serviceProvider, options) => options
            .UseNpgsql(serviceProvider.GetRequiredService<IConfiguration>().GetConnectionString("CatalogDb"))
            .UseSnakeCaseNamingConvention());

        services.AddScoped<IUnitOfWork>(serviceProvider => serviceProvider.GetRequiredService<CatalogDbContext>());
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IProductQueries, ProductQueries>();

        services.AddKafkaMessaging<CatalogDbContext>(configuration);

        AddRedisCache(services, configuration);

        services.AddHealthChecks()
            .AddDbContextCheck<CatalogDbContext>("postgres")
            .AddRedis(serviceProvider => serviceProvider.GetRequiredService<IConnectionMultiplexer>(), "redis");

        return services;
    }

    private static void AddRedisCache(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IConnectionMultiplexer>(serviceProvider =>
        {
            var connectionString = serviceProvider.GetRequiredService<IConfiguration>().GetConnectionString("Redis")
                ?? throw new InvalidOperationException("Connection string 'Redis' is not configured.");

            var redisOptions = ConfigurationOptions.Parse(connectionString);
            redisOptions.AbortOnConnectFail = false;
            return ConnectionMultiplexer.Connect(redisOptions);
        });

        services.AddStackExchangeRedisCache(options => options.InstanceName = "catalog:");
        services.AddOptions<RedisCacheOptions>()
            .Configure<IServiceProvider>((options, serviceProvider) => options.ConnectionMultiplexerFactory =
                () => Task.FromResult(serviceProvider.GetRequiredService<IConnectionMultiplexer>()));

        services.AddOptions<ProductCacheOptions>().Bind(configuration.GetSection(ProductCacheOptions.SectionName));
        services.AddSingleton<IProductCache, RedisProductCache>();

        // Redis commands become spans in the same distributed trace.
        services.ConfigureOpenTelemetryTracerProvider(tracing => tracing.AddRedisInstrumentation());
    }
}
