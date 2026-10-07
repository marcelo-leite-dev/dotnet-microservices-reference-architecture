namespace BuildingBlocks.Application.Abstractions;

/// <summary>
/// Commits every change tracked during a use case atomically: aggregates, outbox messages and the
/// side effects of domain event handlers all succeed or fail together.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
