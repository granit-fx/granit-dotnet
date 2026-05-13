using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Granit.Observability;

/// <summary>
/// Process-global registry of OpenTelemetry tracing/metrics contributors declared
/// by Granit modules that embed third-party SDKs (Redis, Npgsql, AWS, ...).
/// </summary>
/// <remarks>
/// <para>
/// This registry preserves the layering rule that <c>Granit.Observability</c> must NOT
/// take a hard dependency on third-party instrumentation packages. Modules that embed
/// a SDK (e.g. <c>Granit.Caching.StackExchangeRedis</c>) own their own
/// <c>OpenTelemetry.Instrumentation.*</c> package and contribute their
/// <see cref="TracerProviderBuilder"/> / <see cref="MeterProviderBuilder"/> wiring here
/// during host configuration. <c>Granit.Observability</c> then iterates the contributors
/// inside <c>WithTracing()</c> / <c>WithMetrics()</c> — coupling stays unidirectional
/// (contributors → registry), never the inverse.
/// </para>
/// <para>
/// The registry is static and process-global, matching the lifetime of the OTel SDK
/// providers it feeds.
/// </para>
/// </remarks>
public static class GranitOpenTelemetryRegistry
{
    private static readonly Lock SyncLock = new();
    private static readonly List<Action<TracerProviderBuilder>> TracingContributors = [];
    private static readonly List<Action<MeterProviderBuilder>> MetricsContributors = [];

    /// <summary>
    /// Registers a tracing contributor applied to the <see cref="TracerProviderBuilder"/>
    /// when <c>AddGranitObservability()</c> wires the OpenTelemetry tracer.
    /// </summary>
    public static void RegisterTracing(Action<TracerProviderBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        lock (SyncLock) { TracingContributors.Add(configure); }
    }

    /// <summary>
    /// Registers a metrics contributor applied to the <see cref="MeterProviderBuilder"/>
    /// when <c>AddGranitObservability()</c> wires the OpenTelemetry meter.
    /// </summary>
    public static void RegisterMetrics(Action<MeterProviderBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        lock (SyncLock) { MetricsContributors.Add(configure); }
    }

    /// <summary>Returns a snapshot of all tracing contributors.</summary>
    public static IReadOnlyCollection<Action<TracerProviderBuilder>> GetTracingContributors()
    {
        lock (SyncLock) { return [.. TracingContributors]; }
    }

    /// <summary>Returns a snapshot of all metrics contributors.</summary>
    public static IReadOnlyCollection<Action<MeterProviderBuilder>> GetMetricsContributors()
    {
        lock (SyncLock) { return [.. MetricsContributors]; }
    }
}
