using Granit.Diagnostics;
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

    // OpenTelemetry SDK 1.9+ enforces mutual exclusion at TracerProvider/MeterProvider
    // build time: a service collection may carry either a single cross-cutting
    // UseOtlpExporter() registration OR per-signal AddOtlpExporter() registrations,
    // but not both (NotSupportedException is thrown otherwise). UseOtlpExporter()
    // marks its presence by registering this singleton — we probe for it by full
    // type name because the type itself is internal to the SDK.
    private const string UseOtlpExporterMarkerTypeFullName =
        "OpenTelemetry.Exporter.UseOtlpExporterRegistration";

    private static void ConfigureOpenTelemetry(IHostApplicationBuilder builder, ObservabilityOptions options)
    {
        // Detect whether the host (e.g. .NET Aspire's ServiceDefaults) has already wired
        // OpenTelemetry's cross-cutting UseOtlpExporter(). If so, Granit must defer
        // entirely: registering per-signal AddOtlpExporter() in addition would trip the
        // SDK's mutual-exclusion guard at TracerProvider build time.
        //
        // This relies on call ordering: AddServiceDefaults() (or any host UseOtlpExporter())
        // MUST run before AddGranitObservability(). The microservice-template enforces
        // this in SharedHostingExtensions.AddSharedHostingAsync.
        //
        // When no host registration is present, Granit owns OTLP — covering vanilla
        // k8s/Docker/shell setups that rely on OTEL_EXPORTER_OTLP_ENDPOINT (picked up
        // by ApplyFallbacks → options.OtlpEndpoint) without an Aspire-style host call.
        bool deferOtlpToHost = builder.Services.Any(d =>
            d.ServiceType.FullName == UseOtlpExporterMarkerTypeFullName);

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(
                serviceName: options.ServiceName,
                serviceNamespace: options.ServiceNamespace,
                serviceVersion: options.ServiceVersion))
            .WithTracing(tracing => ConfigureTracing(tracing, options, deferOtlpToHost))
            .WithMetrics(metrics => ConfigureMetrics(metrics, options, deferOtlpToHost));
    }

    private static void ConfigureTracing(TracerProviderBuilder tracing, ObservabilityOptions options, bool deferOtlpToHost)
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

        if (!deferOtlpToHost)
        {
            tracing.AddOtlpExporter(otlp => otlp.Endpoint = new Uri(options.OtlpEndpoint));
        }
    }

    private static void ConfigureMetrics(MeterProviderBuilder metrics, ObservabilityOptions options, bool deferOtlpToHost)
    {
        if (!options.EnableMetrics)
        {
            return;
        }

        metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            // Every Granit module meter is created via IMeterFactory with the name
            // "Granit.<Package>" (see the *Metrics classes). Subscribe to them all by
            // wildcard so module metrics export with no per-module registration — the
            // metrics counterpart to GranitActivitySourceRegistry on the tracing side,
            // but self-maintaining: a new *Metrics class needs no Observability change.
            .AddMeter("Granit.*");

        // Apply metrics contributors declared by SDK-embedding modules.
        foreach (Action<MeterProviderBuilder> contributor in
            GranitOpenTelemetryRegistry.GetMetricsContributors())
        {
            contributor(metrics);
        }

        if (!deferOtlpToHost)
        {
            metrics.AddOtlpExporter(otlp => otlp.Endpoint = new Uri(options.OtlpEndpoint));
        }
    }
}
