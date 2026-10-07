using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Infrastructure.Persistence;

public static class DatabaseMigrationExtensions
{
    private const int MaxAttempts = 10;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Applies pending EF Core migrations when <c>Database:MigrateOnStartup</c> is enabled.
    /// Convenient for local development and demos; production deployments should prefer a migration
    /// bundle executed by a dedicated job (see ADR-0007).
    /// </summary>
    public static async Task MigrateDatabaseAsync<TContext>(this IHost host, CancellationToken cancellationToken = default)
        where TContext : DbContext
    {
        var configuration = host.Services.GetRequiredService<IConfiguration>();
        if (!configuration.GetValue("Database:MigrateOnStartup", false))
        {
            return;
        }

        var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseMigration");

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await using var scope = host.Services.CreateAsyncScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<TContext>();
                await dbContext.Database.MigrateAsync(cancellationToken);
                logger.LogInformation("Database migrations for {DbContext} applied", typeof(TContext).Name);
                return;
            }
            catch (Exception exception) when (attempt < MaxAttempts && exception is not OperationCanceledException)
            {
                logger.LogWarning(
                    exception,
                    "Applying migrations for {DbContext} failed (attempt {Attempt}/{MaxAttempts}); retrying in {Delay}",
                    typeof(TContext).Name,
                    attempt,
                    MaxAttempts,
                    RetryDelay);
                await Task.Delay(RetryDelay, cancellationToken);
            }
        }
    }
}
