using System.Diagnostics;
using BuildingBlocks.Application.Cqrs;
using BuildingBlocks.Application.Diagnostics;
using BuildingBlocks.Domain;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Application.Behaviors;

/// <summary>
/// Outermost behavior: opens a span per command/query, records its duration and logs failures.
/// This makes every use case visible in traces and dashboards without touching handlers.
/// </summary>
public sealed class InstrumentationBehavior<TRequest, TResult>(ILogger<InstrumentationBehavior<TRequest, TResult>> logger)
    : IPipelineBehavior<TRequest, TResult>
    where TRequest : IRequest<TResult>
{
    public async Task<TResult> Handle(
        TRequest request,
        RequestHandlerDelegate<TResult> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        using var activity = ApplicationDiagnostics.ActivitySource.StartActivity(requestName);
        activity?.SetTag("app.request.name", requestName);

        var startedAt = Stopwatch.GetTimestamp();
        var outcome = "success";

        try
        {
            var result = await next();

            if (result is Result { IsFailure: true } failed)
            {
                outcome = "failure";
                activity?.SetStatus(ActivityStatusCode.Error, failed.Error.Code);
                logger.LogWarning(
                    "{RequestName} failed with {ErrorCode}: {ErrorDescription}",
                    requestName,
                    failed.Error.Code,
                    failed.Error.Description);
            }

            return result;
        }
        catch (Exception exception)
        {
            outcome = "exception";
            activity?.SetStatus(ActivityStatusCode.Error, exception.GetType().Name);
            throw;
        }
        finally
        {
            var elapsed = Stopwatch.GetElapsedTime(startedAt);
            ApplicationDiagnostics.RequestDuration.Record(
                elapsed.TotalMilliseconds,
                new KeyValuePair<string, object?>("app.request.name", requestName),
                new KeyValuePair<string, object?>("app.request.outcome", outcome));

            logger.LogDebug("{RequestName} completed in {ElapsedMilliseconds:0.0} ms", requestName, elapsed.TotalMilliseconds);
        }
    }
}
