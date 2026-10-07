using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Cqrs;
using BuildingBlocks.Domain;
using Catalog.Application.Abstractions;
using Catalog.Domain.Products;
using FluentValidation;

namespace Catalog.Application.Products;

public sealed record RestockProductCommand(Guid ProductId, int Quantity) : ICommand<Result>;

internal sealed class RestockProductCommandValidator : AbstractValidator<RestockProductCommand>
{
    public RestockProductCommandValidator()
    {
        RuleFor(c => c.ProductId).NotEmpty();
        RuleFor(c => c.Quantity).InclusiveBetween(1, 100_000);
    }
}

internal sealed class RestockProductCommandHandler(IProductRepository products, IProductCache cache, IUnitOfWork unitOfWork)
    : ICommandHandler<RestockProductCommand, Result>
{
    public async Task<Result> Handle(RestockProductCommand command, CancellationToken cancellationToken)
    {
        var product = await products.GetByIdAsync(command.ProductId, cancellationToken);
        if (product is null)
        {
            return ProductErrors.NotFound(command.ProductId);
        }

        var result = product.Restock(command.Quantity);
        if (result.IsFailure)
        {
            return result;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await cache.RemoveAsync(product.Id, cancellationToken);

        return Result.Success();
    }
}
