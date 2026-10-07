using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Ordering.Application.Abstractions;
using Ordering.Domain.Orders;
using Ordering.Infrastructure.Http;
using Ordering.Infrastructure.Persistence;

namespace Ordering.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddOrderingInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Connection strings are resolved lazily so that hosts (and tests) can override configuration late.
        services.AddDbContext<OrderingDbContext>((serviceProvider, options) => options
            .UseNpgsql(serviceProvider.GetRequiredService<IConfiguration>().GetConnectionString("OrderingDb"))
            .UseSnakeCaseNamingConvention());

        services.AddScoped<IUnitOfWork>(serviceProvider => serviceProvider.GetRequiredService<OrderingDbContext>());
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IOrderQueries, OrderQueries>();

        services.AddKafkaMessaging<OrderingDbContext>(configuration);

        services.AddOptions<CatalogClientOptions>()
            .Bind(configuration.GetSection(CatalogClientOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddHttpClient<ICatalogClient, CatalogHttpClient>((serviceProvider, client) =>
            {
                var baseUrl = serviceProvider.GetRequiredService<IOptions<CatalogClientOptions>>().Value.BaseUrl;
                client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
            })
            .AddStandardResilienceHandler();

        services.AddHealthChecks().AddDbContextCheck<OrderingDbContext>("postgres");

        return services;
    }
}
