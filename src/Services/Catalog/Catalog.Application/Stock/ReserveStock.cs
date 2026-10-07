using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Cqrs;
using BuildingBlocks.Domain;
using BuildingBlocks.Messaging;
using Catalog.Application.Abstractions;
using Catalog.Contracts;
using Catalog.Domain.Products;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace Catalog.Application.Stock;

public sealed record ReserveStockItem(Guid ProductId, int Quantity);

/// <summary>Reserves stock for every item of an order, all or nothing.</summary>
public sealed record ReserveStockCommand(Guid OrderId, IReadOnlyList<ReserveStockItem> Items) : ICommand<Result>;

internal sealed class ReserveStockCommandValidator : AbstractValidator<ReserveStockCommand>
{
    public ReserveStockCommandValidator()
    {
        RuleFor(c => c.OrderId).NotEmpty();
        RuleFor(c => c.Items).NotEmpty();
        RuleForEach(c => c.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.ProductId).NotEmpty();
            item.RuleFor(i => i.Quantity).GreaterThan(0);
        });
    }
}

internal sealed class ReserveStockCommandHandler(
    IProductRepository products,
    IProductCache cache,
    IOutbox outbox,
    IUnitOfWork unitOfWork,
    ILogger<ReserveStockCommandHandler> logger) : ICommandHandler<ReserveStockCommand, Result>
{
    public async Task<Result> Handle(ReserveStockCommand command, CancellationToken cancellationToken)
    {
        var requested = command.Items
            .GroupBy(item => item.ProductId)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.Quantity));

        var found = (await products.GetByIdsAsync(requested.Keys, cancellationToken)).ToDictionary(product => product.Id);
        var partitionKey = command.OrderId.ToString();

        // Check everything first so that a rejected order never leaves partial reservations behind.
        var error = FindReservationError(requested, found);
        if (error is not null)
        {
            outbox.Enqueue(
                new StockReservationFailedIntegrationEvent(command.OrderId, error.Description),
                CatalogTopics.StockReservations,
                partitionKey);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Stock reservation for order {OrderId} rejected: {Reason}", command.OrderId, error.Description);
            return error;
        }

        foreach (var (productId, quantity) in requested)
        {
            found[productId].RemoveStock(quantity);
        }

        outbox.Enqueue(new StockReservedIntegrationEvent(command.OrderId), CatalogTopics.StockReservations, partitionKey);

        // Concurrent reservations of the same product are detected by optimistic concurrency (xmin)
        // and retried by the consumer with fresh data.
        await unitOfWork.SaveChangesAsync(cancellationToken);

        foreach (var productId in requested.Keys)
        {
            await cache.RemoveAsync(productId, cancellationToken);
        }

        logger.LogInformation("Stock reserved for order {OrderId} ({ProductCount} products)", command.OrderId, requested.Count);
        return Result.Success();
    }

    private static Error? FindReservationError(Dictionary<Guid, int> requested, Dictionary<Guid, Product> found)
    {
        foreach (var (productId, quantity) in requested)
        {
            if (!found.TryGetValue(productId, out var product))
            {
                return ProductErrors.NotFound(productId);
            }

            if (!product.HasStock(quantity))
            {
                return ProductErrors.InsufficientStock(productId, quantity, product.AvailableStock);
            }
        }

        return null;
    }
}
