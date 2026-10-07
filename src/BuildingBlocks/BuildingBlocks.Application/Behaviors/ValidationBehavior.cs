using BuildingBlocks.Application.Cqrs;
using FluentValidation;

namespace BuildingBlocks.Application.Behaviors;

/// <summary>
/// Runs every FluentValidation validator registered for the request before the handler executes.
/// Invalid input is a client error, so it surfaces as a <see cref="ValidationException"/> that the
/// web layer translates into an RFC 7807 validation problem response.
/// </summary>
public sealed class ValidationBehavior<TRequest, TResult>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResult>
    where TRequest : IRequest<TResult>
{
    public async Task<TResult> Handle(
        TRequest request,
        RequestHandlerDelegate<TResult> next,
        CancellationToken cancellationToken)
    {
        var activeValidators = validators as IValidator<TRequest>[] ?? validators.ToArray();
        if (activeValidators.Length == 0)
        {
            return await next();
        }

        var context = new ValidationContext<TRequest>(request);
        var results = await Task.WhenAll(activeValidators.Select(v => v.ValidateAsync(context, cancellationToken)));
        var failures = results.SelectMany(r => r.Errors).Where(f => f is not null).ToList();

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }

        return await next();
    }
}
