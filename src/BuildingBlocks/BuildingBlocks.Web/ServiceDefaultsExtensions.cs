using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Npgsql;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Formatting.Compact;

namespace BuildingBlocks.Web;

/// <summary>
/// Opinionated defaults applied to every service so that all of them are observable and behave the
/// same way at the edges (errors, health, logging) — the "paved road" of the platform.
/// </summary>
public static class ServiceDefaultsExtensions
{
    private const string LiveTag = "live";

    /// <param name="builder">The web application builder.</param>
    /// <param name="telemetrySources">Service-specific ActivitySource / Meter names (wildcards allowed).</param>
    public static WebApplicationBuilder AddServiceDefaults(this WebApplicationBuilder builder, params string[] telemetrySources)
    {
        var serviceName = builder.Configuration["Service:Name"] ?? builder.Environment.ApplicationName;

        builder.AddStructuredLogging(serviceName);
        builder.AddOpenTelemetry(serviceName, telemetrySources);

        builder.Services.AddHealthChecks().AddCheck("self", () => HealthCheckResult.Healthy(), tags: [LiveTag]);

        builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
        {
            context.ProblemDetails.Instance ??= context.HttpContext.Request.Path;
            context.ProblemDetails.Extensions["traceId"] = Activity.Current?.TraceId.ToString() ?? context.HttpContext.TraceIdentifier;
        });
        builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(options => options.CustomSchemaIds(type => type.FullName?.Replace('+', '.')));

        return builder;
    }

    public static WebApplication UseServiceDefaults(this WebApplication app)
    {
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        app.UseSerilogRequestLogging();

        if (app.Environment.IsDevelopment() || app.Configuration.GetValue("Swagger:Enabled", false))
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        // Liveness: the process is up. Readiness: dependencies (database, cache) are reachable.
        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = check => check.Tags.Contains(LiveTag) });
        app.MapHealthChecks("/health/ready");

        return app;
    }

    private static void AddStructuredLogging(this WebApplicationBuilder builder, string serviceName)
    {
        var logstashUrl = builder.Configuration["Logstash:Url"];

        builder.Services.AddSerilog((services, logger) =>
        {
            logger.ReadFrom.Configuration(builder.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("ServiceName", serviceName)
                .Enrich.WithProperty("Environment", builder.Environment.EnvironmentName);

            if (builder.Environment.IsDevelopment())
            {
                logger.WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}");
            }
            else
            {
                // JSON to stdout: the format container log collectors expect (12-factor logs).
                logger.WriteTo.Console(new RenderedCompactJsonFormatter());
            }

            if (!string.IsNullOrWhiteSpace(logstashUrl))
            {
                // Compact JSON carries @tr/@sp (trace/span ids), linking logs in Kibana to traces in Jaeger.
                logger.WriteTo.Http(
                    requestUri: logstashUrl,
                    queueLimitBytes: null,
                    textFormatter: new RenderedCompactJsonFormatter());
            }
        });
    }

    private static void AddOpenTelemetry(this WebApplicationBuilder builder, string serviceName, string[] telemetrySources)
    {
        string[] sources = ["BuildingBlocks.*", .. telemetrySources];
        var logstashHost = Uri.TryCreate(builder.Configuration["Logstash:Url"], UriKind.Absolute, out var uri) ? uri.Authority : null;

        var openTelemetry = builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(
                    serviceName,
                    serviceVersion: typeof(ServiceDefaultsExtensions).Assembly.GetName().Version?.ToString(),
                    serviceInstanceId: Environment.MachineName)
                .AddAttributes([new KeyValuePair<string, object>("deployment.environment", builder.Environment.EnvironmentName)]))
            .WithTracing(tracing => tracing
                .AddSource(sources)
                .AddAspNetCoreInstrumentation(options =>
                    options.Filter = context => !context.Request.Path.StartsWithSegments("/health"))
                .AddHttpClientInstrumentation(options =>
                    // Do not trace the log shipper itself.
                    options.FilterHttpRequestMessage = request => request.RequestUri?.Authority != logstashHost)
                .AddNpgsql())
            .WithMetrics(metrics => metrics
                .AddMeter(sources)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation());

        // Export only when a collector is configured (OTEL_EXPORTER_OTLP_ENDPOINT), e.g. not in tests.
        if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
        {
            openTelemetry.UseOtlpExporter();
        }
    }
}
