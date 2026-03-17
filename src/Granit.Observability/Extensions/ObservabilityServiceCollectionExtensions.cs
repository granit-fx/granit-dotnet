using Granit.Core.Diagnostics;
using Granit.Observability.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;

namespace Granit.Observability.Extensions;

/// <summary>
/// Extensions for configuring full observability (logs, traces, metrics).
/// </summary>
public static class ObservabilityServiceCollectionExtensions
{
    /// <summary>
    /// Adds Serilog (structured logs) and OpenTelemetry (traces + metrics)
    /// with OTLP export to the LGTM stack.
    /// </summary>
    public static IHostApplicationBuilder AddGranitObservability(
        this IHostApplicationBuilder builder)
    {
        builder.Services
            .AddOptions<ObservabilityOptions>()
            .BindConfiguration(ObservabilityOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Read options directly from IConfiguration: the DI container is not yet
        // built at this point, so IOptions<> is not resolvable inside Serilog/OTel configuration.
        ObservabilityOptions options = new();
        builder.Configuration
            .GetSection(ObservabilityOptions.SectionName)
            .Bind(options);

        ConfigureSerilog(builder, options);
        ConfigureOpenTelemetry(builder, options);

        return builder;
    }

    private static void ConfigureSerilog(IHostApplicationBuilder builder, ObservabilityOptions options)
    {
        builder.Services.AddSerilog(config =>
        {
            config
                .ReadFrom.Configuration(builder.Configuration)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("ServiceName", options.ServiceName)
                .Enrich.WithProperty("ServiceVersion", options.ServiceVersion)
                .Enrich.WithProperty("Environment", options.Environment)
                .WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}")
                .WriteTo.OpenTelemetry(otel =>
                {
                    otel.Endpoint = options.OtlpEndpoint;
                    otel.ResourceAttributes = new Dictionary<string, object>
                    {
                        ["service.name"] = options.ServiceName,
                        ["service.version"] = options.ServiceVersion,
                        ["service.namespace"] = options.ServiceNamespace,
                        ["deployment.environment"] = options.Environment
                    };
                });
        });
    }

    private static void ConfigureOpenTelemetry(IHostApplicationBuilder builder, ObservabilityOptions options)
    {
        // When OTEL_EXPORTER_OTLP_ENDPOINT is set (injected by .NET Aspire, picked up by
        // ServiceDefaults.UseOtlpExporter()), mixing signal-specific AddOtlpExporter() on the
        // same IServiceCollection is forbidden by OpenTelemetry SDK 1.9+.
        // Skip the Granit-specific OTLP exporters — the cross-cutting one covers all signals.
        bool crossCuttingOtlpActive = !string.IsNullOrWhiteSpace(
            builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(
                serviceName: options.ServiceName,
                serviceVersion: options.ServiceVersion,
                serviceNamespace: options.ServiceNamespace))
            .WithTracing(tracing =>
            {
                if (!options.EnableTracing)
                {
                    return;
                }

                // Register all Granit module ActivitySources declared via
                // GranitActivitySourceRegistry.Register() during host configuration.
                foreach (string source in GranitActivitySourceRegistry.GetRegisteredSources())
                {
                    tracing.AddSource(source);
                }

                tracing
                    .AddAspNetCoreInstrumentation(aspnet =>
                    {
                        aspnet.RecordException = true;
                        aspnet.Filter = httpContext =>
                        {
                            Microsoft.AspNetCore.Http.PathString path = httpContext.Request.Path;
                            // Exclude /health/* (liveness, readiness, startup) and /healthz (legacy).
                            // StartsWithSegments is segment-safe: /healthcare/... is NOT excluded.
                            return !path.StartsWithSegments("/health") && path != "/healthz";
                        };
                    })
                    .AddHttpClientInstrumentation()
                    .AddEntityFrameworkCoreInstrumentation();

                if (!crossCuttingOtlpActive)
                {
                    tracing.AddOtlpExporter(otlp => otlp.Endpoint = new Uri(options.OtlpEndpoint));
                }
            })
            .WithMetrics(metrics =>
            {
                if (!options.EnableMetrics)
                {
                    return;
                }

                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation();

                if (!crossCuttingOtlpActive)
                {
                    metrics.AddOtlpExporter(otlp => otlp.Endpoint = new Uri(options.OtlpEndpoint));
                }
            });
    }
}
