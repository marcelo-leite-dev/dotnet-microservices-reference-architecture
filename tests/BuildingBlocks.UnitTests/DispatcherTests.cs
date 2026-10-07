using BuildingBlocks.Application;
using BuildingBlocks.Application.Cqrs;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace BuildingBlocks.UnitTests;

public sealed class DispatcherTests
{
    [Fact]
    public async Task Send_RoutesRequestToItsHandler()
    {
        await using var provider = BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();

        var reply = await dispatcher.Send(new Ping("hello"));

        Assert.Equal("pong: hello", reply);
    }

    [Fact]
    public async Task Send_WhenRequestIsInvalid_ThrowsValidationExceptionWithoutCallingHandler()
    {
        await using var provider = BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();

        var exception = await Assert.ThrowsAsync<ValidationException>(() => dispatcher.Send(new Ping(string.Empty)));

        Assert.Contains(exception.Errors, failure => failure.PropertyName == nameof(Ping.Message));
    }

    [Fact]
    public async Task Send_RunsBehaviorsInRegistrationOrder_AroundTheHandler()
    {
        var journal = new List<string>();
        await using var provider = BuildServiceProvider(services =>
        {
            services.AddSingleton(journal);
            services.AddScoped<IPipelineBehavior<Ping, string>, JournalingBehavior>(sp => new JournalingBehavior("outer", sp.GetRequiredService<List<string>>()));
            services.AddScoped<IPipelineBehavior<Ping, string>, JournalingBehavior>(sp => new JournalingBehavior("inner", sp.GetRequiredService<List<string>>()));
        });

        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IDispatcher>().Send(new Ping("order"));

        Assert.Equal(new[] { "outer:before", "inner:before", "inner:after", "outer:after" }, journal);
    }

    private static ServiceProvider BuildServiceProvider(Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddApplicationCore(typeof(DispatcherTests).Assembly);
        configure?.Invoke(services);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    internal sealed record Ping(string Message) : ICommand<string>;

    internal sealed class PingHandler : ICommandHandler<Ping, string>
    {
        public Task<string> Handle(Ping request, CancellationToken cancellationToken) => Task.FromResult($"pong: {request.Message}");
    }

    internal sealed class PingValidator : AbstractValidator<Ping>
    {
        public PingValidator() => RuleFor(p => p.Message).NotEmpty();
    }

    private sealed class JournalingBehavior(string name, List<string> journal) : IPipelineBehavior<Ping, string>
    {
        public async Task<string> Handle(Ping request, RequestHandlerDelegate<string> next, CancellationToken cancellationToken)
        {
            journal.Add($"{name}:before");
            var result = await next();
            journal.Add($"{name}:after");
            return result;
        }
    }
}
