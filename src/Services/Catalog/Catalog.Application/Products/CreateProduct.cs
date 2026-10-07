using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Cqrs;
using BuildingBlocks.Domain;
using Catalog.Domain.Products;
using FluentValidation;

namespace Catalog.Application.Products;

public sealed record CreateProductCommand(
    string Sku,
    string Name,
    string? Description,
    decimal Price,
    int InitialStock) : ICommand<Result<Guid>>;

internal sealed class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        RuleFor(c => c.Sku).NotEmpty().MaximumLength(Product.SkuMaxLength);
        RuleFor(c => c.Name).NotEmpty().MaximumLength(Product.NameMaxLength);
        RuleFor(c => c.Description).MaximumLength(Product.DescriptionMaxLength);
        RuleFor(c => c.Price).GreaterThan(0).PrecisionScale(18, 2, ignoreTrailingZeros: true);
        RuleFor(c => c.InitialStock).GreaterThanOrEqualTo(0);
    }
}

internal sealed class CreateProductCommandHandler(IProductRepository products, IUnitOfWork unitOfWork)
    : ICommandHandler<CreateProductCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateProductCommand command, CancellationToken cancellationToken)
    {
        if (await products.SkuExistsAsync(command.Sku, cancellationToken))
        {
            return ProductErrors.DuplicateSku(Product.NormalizeSku(command.Sku));
        }

        var result = Product.Create(command.Sku, command.Name, command.Description, command.Price, command.InitialStock);
        if (result.IsFailure)
        {
            return result.Error;
        }

        products.Add(result.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return result.Value.Id;
    }
}
