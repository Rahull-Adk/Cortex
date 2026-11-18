using Cortex.Application.Common;
using Npgsql;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Cortex.Api;

public static class Telemetry
{
    private static readonly string[] IgnoredPathPrefixes = ["/scalar", "/openapi"];

    public static WebApplicationBuilder AddCortexTelemetry(this WebApplicationBuilder builder)
    {
        var serviceName = builder.Configuration["OTEL_SERVICE_NAME"] ?? "cortex-api";
        var serviceVersion = typeof(Telemetry).Assembly.GetName().Version?.ToString();

        builder.Logging.AddOpenTelemetry(options =>
        {
            options.IncludeScopes = true;
            options.IncludeFormattedMessage = true;
        });

        var otel = builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(serviceName, serviceVersion: serviceVersion)
                .AddAttributes([
                    new("deployment.environment.name", builder.Environment.EnvironmentName),
                ]))
            .WithTracing(tracing => tracing
                .AddSource(CortexTelemetry.Name)
                .AddAspNetCoreInstrumentation(options =>
                    options.Filter = context => !IgnoredPathPrefixes.Any(prefix =>
                        context.Request.Path.StartsWithSegments(prefix)))
                .AddHttpClientInstrumentation()
                .AddNpgsql())
            .WithMetrics(metrics => metrics
                .AddMeter(CortexTelemetry.Name)
                .AddMeter("Npgsql")
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation());

        // Without a collector the API runs as before and nothing is exported.
        if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
            otel.UseOtlpExporter();

        return builder;
    }
}
