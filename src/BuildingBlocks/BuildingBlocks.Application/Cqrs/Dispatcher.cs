using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.Application.Cqrs;

/// <summary>
/// Minimal mediator: resolves the single handler of a request plus every pipeline behavior and
/// chains them. Reflection is used once per request type; the resulting wrapper is cached.
/// </summary>
internal sealed class Dispatcher(IServiceProvider serviceProvider) : IDispatcher
{
    private static readonly ConcurrentDictionary<Type, RequestHandlerWrapper> Wrappers = new();

    public Task<TResult> Send<TResult>(IRequest<TResult> request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var wrapper = (RequestHandlerWrapper<TResult>)Wrappers.GetOrAdd(
            request.GetType(),
            static requestType => (RequestHandlerWrapper)Activator.CreateInstance(
                typeof(RequestHandlerWrapperImpl<,>).MakeGenericType(requestType, typeof(TResult)))!);

        return wrapper.Handle(request, serviceProvider, cancellationToken);
    }

    private abstract class RequestHandlerWrapper;

    private abstract class RequestHandlerWrapper<TResult> : RequestHandlerWrapper
    {
        public abstract Task<TResult> Handle(
            IRequest<TResult> request,
            IServiceProvider serviceProvider,
            CancellationToken cancellationToken);
    }

    private sealed class RequestHandlerWrapperImpl<TRequest, TResult> : RequestHandlerWrapper<TResult>
        where TRequest : IRequest<TResult>
    {
        public override Task<TResult> Handle(
            IRequest<TResult> request,
            IServiceProvider serviceProvider,
            CancellationToken cancellationToken)
        {
            var typedRequest = (TRequest)request;
            var handler = serviceProvider.GetRequiredService<IRequestHandler<TRequest, TResult>>();

            RequestHandlerDelegate<TResult> pipeline = () => handler.Handle(typedRequest, cancellationToken);

            foreach (var behavior in serviceProvider.GetServices<IPipelineBehavior<TRequest, TResult>>().Reverse())
            {
                var next = pipeline;
                pipeline = () => behavior.Handle(typedRequest, next, cancellationToken);
            }

            return pipeline();
        }
    }
}
