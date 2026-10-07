using BuildingBlocks.Infrastructure.Persistence;
using BuildingBlocks.Web;
using Ordering.Api.Endpoints;
using Ordering.Application;
using Ordering.Infrastructure;
using Ordering.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults("Ordering.*");

builder.Services
    .AddOrderingApplication()
    .AddOrderingInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseServiceDefaults();
app.MapOrderEndpoints();

await app.MigrateDatabaseAsync<OrderingDbContext>();

app.Run();

// Exposes the entry point to WebApplicationFactory in integration tests.
public partial class Program;
