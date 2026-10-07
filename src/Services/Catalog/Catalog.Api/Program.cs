using BuildingBlocks.Infrastructure.Persistence;
using BuildingBlocks.Web;
using Catalog.Api.Endpoints;
using Catalog.Application;
using Catalog.Infrastructure;
using Catalog.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults("Catalog.*");

builder.Services
    .AddCatalogApplication()
    .AddCatalogInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseServiceDefaults();
app.MapProductEndpoints();

await app.MigrateDatabaseAsync<CatalogDbContext>();

app.Run();

// Exposes the entry point to WebApplicationFactory in integration tests.
public partial class Program;
