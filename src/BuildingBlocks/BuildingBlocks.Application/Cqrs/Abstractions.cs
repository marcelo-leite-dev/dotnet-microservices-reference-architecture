namespace BuildingBlocks.Application.Cqrs;

/// <summary>Marker for any message handled by exactly one handler and producing a <typeparamref name="TResult"/>.</summary>
public interface IRequest<TResult>
{
}

/// <summary>A request that changes state. Commands are handled by exactly one handler.</summary>
public interface ICommand<TResult> : IRequest<TResult>
{
}

/// <summary>A request that reads state and has no side effects.</summary>
public interface IQuery<TResult> : IRequest<TResult>
{
}

public interface IRequestHandler<in TRequest, TResult>
    where TRequest : IRequest<TResult>
{
    Task<TResult> Handle(TRequest request, CancellationToken cancellationToken);
}

public interface ICommandHandler<in TCommand, TResult> : IRequestHandler<TCommand, TResult>
    where TCommand : ICommand<TResult>
{
}

public interface IQueryHandler<in TQuery, TResult> : IRequestHandler<TQuery, TResult>
    where TQuery : IQuery<TResult>
{
}

public delegate Task<TResult> RequestHandlerDelegate<TResult>();

/// <summary>
/// Cross-cutting concern wrapped around every request handler (validation, tracing, logging, ...).
/// Behaviors run in registration order: the first registered is the outermost.
/// </summary>
public interface IPipelineBehavior<in TRequest, TResult>
    where TRequest : IRequest<TResult>
{
    Task<TResult> Handle(TRequest request, RequestHandlerDelegate<TResult> next, CancellationToken cancellationToken);
}

/// <summary>Sends a command or query to its handler through the pipeline behaviors.</summary>
public interface IDispatcher
{
    Task<TResult> Send<TResult>(IRequest<TResult> request, CancellationToken cancellationToken = default);
}
