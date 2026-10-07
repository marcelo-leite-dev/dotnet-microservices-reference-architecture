using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Cqrs;
using BuildingBlocks.Application.DomainEvents;
using BuildingBlocks.Domain;
using BuildingBlocks.Messaging;
using Catalog.Application.Abstractions;
using Catalog.Contracts;
using Catalog.Domain.Products;
using FluentValidation;

namespace Catalog.Application.Products;

public sealed record ChangeProductPriceCommand(Guid ProductId, decimal NewPrice) : ICommand<Result>;

internal sealed class ChangeProductPriceCommandValidator : AbstractValidator<ChangeProductPriceCommand>
{
    public ChangeProductPriceCommandValidator()
    {
        RuleFor(c => c.ProductId).NotEmpty();
        RuleFor(c => c.NewPrice).GreaterThan(0).PrecisionScale(18, 2, ignoreTrailingZeros: true);
    }
}

internal sealed class ChangeProductPriceCommandHandler(IProductRepository products, IProductCache cache, IUnitOfWork unitOfWork)
    : ICommandHandler<ChangeProductPriceCommand, Result>
{
    public async Task<Result> Handle(ChangeProductPriceCommand command, CancellationToken cancellationToken)
    {
        var product = await products.GetByIdAsync(command.ProductId, cancellationToken);
        if (product is null)
        {
            return ProductErrors.NotFound(command.ProductId);
        }

        var result = product.ChangePrice(command.NewPrice);
        if (result.IsFailure)
        {
            return result;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await cache.RemoveAsync(product.Id, cancellationToken);

        return Result.Success();
    }
}

/// <summary>Translates the internal domain event into a public integration event, atomically, via the outbox.</summary>
internal sealed class ProductPriceChangedDomainEventHandler(IOutbox outbox) : IDomainEventHandler<ProductPriceChangedDomainEvent>
{
    public Task Handle(ProductPriceChangedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        outbox.Enqueue(
            new ProductPriceChangedIntegrationEvent(domainEvent.ProductId, domainEvent.OldPrice, domainEvent.NewPrice),
            CatalogTopics.Products,
            domainEvent.ProductId.ToString());

        return Task.CompletedTask;
    }
}
