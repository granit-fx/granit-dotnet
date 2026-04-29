namespace Granit.Analytics.Internal;

/// <summary>
/// Registry of <see cref="IMapRunner"/>s keyed by query name. Mirrors the
/// other widget-runner registries (Metric / QueryAggregate / Table / Chart /
/// Pivot): single point of resolution so the dashboard render path stays
/// reflection-free at request time.
/// </summary>
internal sealed class MapService(IEnumerable<IMapRunner> runners)
{
    private readonly Dictionary<string, IMapRunner> _byName =
        runners.ToDictionary(r => r.Name, StringComparer.OrdinalIgnoreCase);

    public bool TryGetRunner(string queryName, out IMapRunner runner) =>
        _byName.TryGetValue(queryName, out runner!);
}
