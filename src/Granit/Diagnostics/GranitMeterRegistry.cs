namespace Granit.Diagnostics;

/// <summary>
/// Process-global registry of <see cref="System.Diagnostics.Metrics.Meter"/> names
/// declared by Granit modules. <c>Granit.Observability</c> reads this registry
/// at startup to wire <c>AddMeter()</c> calls automatically.
/// </summary>
/// <remarks>
/// The metrics counterpart to <see cref="GranitActivitySourceRegistry"/>. Granit-owned
/// meters (<c>"Granit.*"</c>) are already covered by a wildcard subscription and never
/// need registration here — this registry exists for meters emitted by embedded
/// third-party libraries under their own namespace (e.g. FusionCache's
/// <c>ZiggyCreatures.Caching.Fusion</c>), which the wildcard cannot see.
/// <para>
/// Modules call <see cref="Register"/> during their <c>AddGranit*()</c> extension method
/// (host configuration phase, before <c>Build()</c>). The registry is static and
/// process-global, matching the lifetime of the OTel meter provider it feeds.
/// </para>
/// </remarks>
public static class GranitMeterRegistry
{
    private static readonly Lock SyncLock = new();
    private static readonly HashSet<string> Meters = new(StringComparer.Ordinal);

    /// <summary>
    /// Registers a <see cref="System.Diagnostics.Metrics.Meter"/> name so that
    /// <c>Granit.Observability</c> includes it in the OpenTelemetry meter provider.
    /// </summary>
    /// <param name="meterName">
    /// The meter name (must match the <c>Meter</c> constructor argument).
    /// </param>
    public static void Register(string meterName)
    {
        ArgumentNullException.ThrowIfNull(meterName);
        lock (SyncLock) { Meters.Add(meterName); }
    }

    /// <summary>
    /// Returns a snapshot of all registered meter names.
    /// </summary>
    public static IReadOnlyCollection<string> GetRegisteredMeters()
    {
        lock (SyncLock) { return [.. Meters]; }
    }
}
