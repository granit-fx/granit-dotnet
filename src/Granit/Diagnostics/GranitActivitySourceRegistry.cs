namespace Granit.Diagnostics;

/// <summary>
/// Process-global registry of <see cref="System.Diagnostics.ActivitySource"/> names
/// declared by Granit modules. <c>Granit.Observability</c> reads this registry
/// at startup to wire <c>AddSource()</c> calls automatically.
/// </summary>
/// <remarks>
/// Modules call <see cref="Register"/> during their <c>AddGranit*()</c> extension method
/// (host configuration phase, before <c>Build()</c>). <c>Granit.Observability</c> iterates
/// <see cref="GetRegisteredSources"/> inside <c>WithTracing()</c> — also before <c>Build()</c>.
/// <para>
/// The registry is static and process-global, matching the lifetime of
/// <see cref="System.Diagnostics.ActivitySource"/> itself.
/// </para>
/// </remarks>
public static class GranitActivitySourceRegistry
{
    private static readonly Lock SyncLock = new();
    private static readonly HashSet<string> Sources = new(StringComparer.Ordinal);

    /// <summary>
    /// Registers an <see cref="System.Diagnostics.ActivitySource"/> name so that
    /// <c>Granit.Observability</c> includes it in the OpenTelemetry tracer provider.
    /// </summary>
    /// <param name="sourceName">
    /// The source name (must match the <c>ActivitySource</c> constructor argument).
    /// </param>
    public static void Register(string sourceName)
    {
        ArgumentNullException.ThrowIfNull(sourceName);
        lock (SyncLock) { Sources.Add(sourceName); }
    }

    /// <summary>
    /// Returns a snapshot of all registered source names.
    /// </summary>
    public static IReadOnlyCollection<string> GetRegisteredSources()
    {
        lock (SyncLock) { return [.. Sources]; }
    }
}
