using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Cqrs;
using BuildingBlocks.Domain;
using FluentValidation;
using Ordering.Application.Abstractions;
using Ordering.Domain.Orders;

namespace Ordering.Application.Orders;

public sealed record PlaceOrderItem(Guid ProductId, int Quantity);

public sealed record PlaceOrderCommand(Guid CustomerId, IReadOnlyList<PlaceOrderItem> Items) : ICommand<Result<Guid>>;

internal sealed class PlaceOrderCommandValidator : AbstractValidator<PlaceOrderCommand>
{
    public const int MaxItems = 50;

    public PlaceOrderCommandValidator()
    {
        RuleFor(c => c.CustomerId).NotEmpty();
        RuleFor(c => c.Items).NotEmpty().Must(items => items.Count <= MaxItems)
            .WithMessage($"An order can contain at most {MaxItems} items.");
        RuleForEach(c => c.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.ProductId).NotEmpty();
            item.RuleFor(i => i.Quantity).InclusiveBetween(1, 1_000);
        });
    }
}

/// <summary>
/// Step 1 of the order saga: price the order with a Catalog snapshot, persist it as Pending and
/// (through the OrderPlaced domain event) enqueue the integration event — all in one transaction.
/// </summary>
internal sealed class PlaceOrderCommandHandler(
    ICatalogClient catalog,
    IOrderRepository orders,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<PlaceOrderCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(PlaceOrderCommand command, CancellationToken cancellationToken)
    {
        var productIds = command.Items.Select(item => item.ProductId).Distinct().ToArray();

        var catalogResult = await catalog.GetProductsAsync(productIds, cancellationToken);
        if (catalogResult.IsFailure)
        {
            return catalogResult.Error;
        }

        var products = catalogResult.Value.ToDictionary(product => product.Id);
        var missing = productIds.Where(id => !products.ContainsKey(id)).ToArray();
        if (missing.Length > 0)
        {
            return OrderErrors.ProductsNotFound(missing);
        }

        var lines = command.Items
            .Select(item => new OrderLine(item.ProductId, products[item.ProductId].Name, products[item.ProductId].Price, item.Quantity))
            .ToList();

        var orderResult = Order.Place(command.CustomerId, lines, timeProvider.GetUtcNow().UtcDateTime);
        if (orderResult.IsFailure)
        {
            return orderResult.Error;
        }

        orders.Add(orderResult.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return orderResult.Value.Id;
    }
}
