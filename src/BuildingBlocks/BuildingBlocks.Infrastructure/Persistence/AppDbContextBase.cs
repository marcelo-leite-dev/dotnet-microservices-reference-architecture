using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.DomainEvents;
using BuildingBlocks.Domain;
using BuildingBlocks.Infrastructure.Inbox;
using BuildingBlocks.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;

namespace BuildingBlocks.Infrastructure.Persistence;

/// <summary>
/// Base DbContext for every service. It is the unit of work: before committing, it dispatches the
/// domain events raised by tracked aggregates so their handlers (e.g. writing to the outbox) take
/// part in the same database transaction.
/// </summary>
public abstract class AppDbContextBase : DbContext, IUnitOfWork
{
    private readonly IDomainEventDispatcher _domainEventDispatcher;

    protected AppDbContextBase(DbContextOptions options, IDomainEventDispatcher domainEventDispatcher)
        : base(options) => _domainEventDispatcher = domainEventDispatcher;

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        await DispatchDomainEventsAsync(cancellationToken);
        return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());
        modelBuilder.ApplyConfiguration(new InboxMessageConfiguration());
    }

    private async Task DispatchDomainEventsAsync(CancellationToken cancellationToken)
    {
        // Handlers may change other aggregates and raise further events, so loop until quiescent.
        while (true)
        {
            var aggregates = ChangeTracker.Entries<IHasDomainEvents>()
                .Select(entry => entry.Entity)
                .Where(aggregate => aggregate.DomainEvents.Count > 0)
                .ToList();

            if (aggregates.Count == 0)
            {
                return;
            }

            var domainEvents = aggregates.SelectMany(aggregate => aggregate.DomainEvents).ToList();
            aggregates.ForEach(aggregate => aggregate.ClearDomainEvents());

            foreach (var domainEvent in domainEvents)
            {
                await _domainEventDispatcher.DispatchAsync(domainEvent, cancellationToken);
            }
        }
    }
}
