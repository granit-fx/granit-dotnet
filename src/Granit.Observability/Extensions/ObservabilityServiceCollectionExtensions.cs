using Granit.Diagnostics;
using Granit.Observability.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
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
            // Apply smart fallbacks when the Observability section is absent or incomplete.
            // PostConfigure runs after BindConfiguration so explicit config always wins.
            .PostConfigure<IHostEnvironment, IConfiguration>((opts, env, config) =>
                ApplyFallbacks(opts, env.ApplicationName, env.EnvironmentName,
                    config["OTEL_EXPORTER_OTLP_ENDPOINT"]))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Read options directly from IConfiguration: the DI container is not yet
        // built at this point, so IOptions<> is not resolvable inside Serilog/OTel configuration.
        // Apply the same fallbacks as PostConfigure above.
        ObservabilityOptions options = new();
        builder.Configuration
            .GetSection(ObservabilityOptions.SectionName)
            .Bind(options);

        ApplyFallbacks(options, builder.Environment.ApplicationName,
            builder.Environment.EnvironmentName, builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        ConfigureSerilog(builder, options);
        ConfigureOpenTelemetry(builder, options);

        return builder;
    }

    private static void ApplyFallbacks(
        ObservabilityOptions opts, string appName, string envName, string? otlpEndpointEnv)
    {
        if (string.IsNullOrWhiteSpace(opts.ServiceName) || opts.ServiceName is "unknown-service")
        {
            opts.ServiceName = appName;
        }

        if (string.IsNullOrWhiteSpace(opts.Environment) || opts.Environment is "development")
        {
            opts.Environment = envName.ToLowerInvariant();
        }

        // Respect OTEL_EXPORTER_OTLP_ENDPOINT injected by Aspire when OtlpEndpoint
        // is not explicitly configured (still at default value).
        if (opts.OtlpEndpoint is "http://localhost:4317" && !string.IsNullOrWhiteSpace(otlpEndpointEnv))
        {
            opts.OtlpEndpoint = otlpEndpointEnv;
        }
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

        OpenTelemetry.IOpenTelemetryBuilder otelBuilder = builder.Services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(
                serviceName: options.ServiceName,
                serviceVersion: options.ServiceVersion,
                serviceNamespace: options.ServiceNamespace))
            .WithTracing(tracing => ConfigureTracing(tracing, options, crossCuttingOtlpActive))
            .WithMetrics(metrics => ConfigureMetrics(metrics, options, crossCuttingOtlpActive));

        // When OTEL_EXPORTER_OTLP_ENDPOINT is set (e.g. by .NET Aspire), use the
        // cross-cutting UseOtlpExporter() which exports all signals (traces, metrics,
        // logs) in a single call — avoiding the SDK 1.9+ conflict with per-signal exporters.
        if (crossCuttingOtlpActive)
        {
            otelBuilder.UseOtlpExporter();
        }
    }

    private static void ConfigureTracing(TracerProviderBuilder tracing, ObservabilityOptions options, bool crossCuttingOtlpActive)
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
                aspnet.Filter = static httpContext =>
                {
                    Microsoft.AspNetCore.Http.PathString path = httpContext.Request.Path;
                    // Exclude /health/* (liveness, readiness, startup) and /healthz (legacy).
                    // StartsWithSegments is segment-safe: /healthcare/... is NOT excluded.
                    return !path.StartsWithSegments("/health") && path != "/healthz";
                };
            })
            .AddHttpClientInstrumentation()
            .AddEntityFrameworkCoreInstrumentation();

        // Apply tracing contributors declared by SDK-embedding modules
        // (Redis, Npgsql, AWS, ...). See GranitOpenTelemetryRegistry.
        foreach (Action<TracerProviderBuilder> contributor in
            GranitOpenTelemetryRegistry.GetTracingContributors())
        {
            contributor(tracing);
        }

        if (!crossCuttingOtlpActive)
        {
            tracing.AddOtlpExporter(otlp => otlp.Endpoint = new Uri(options.OtlpEndpoint));
        }
    }

    private static void ConfigureMetrics(MeterProviderBuilder metrics, ObservabilityOptions options, bool crossCuttingOtlpActive)
    {
        if (!options.EnableMetrics)
        {
            return;
        }

        metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation();

        // Apply metrics contributors declared by SDK-embedding modules.
        foreach (Action<MeterProviderBuilder> contributor in
            GranitOpenTelemetryRegistry.GetMetricsContributors())
        {
            contributor(metrics);
        }

        if (!crossCuttingOtlpActive)
        {
            metrics.AddOtlpExporter(otlp => otlp.Endpoint = new Uri(options.OtlpEndpoint));
        }
    }
}
