namespace Granit.Analytics.Internal;

/// <summary>
/// Registry of <see cref="IChartRunner"/>s keyed by query name. Mirrors
/// <see cref="QueryAggregateService"/> / <see cref="TableService"/> for the
/// chart side: a single point of resolution so the dashboard render path
/// stays reflection-free at request time.
/// </summary>
internal sealed class ChartService(IEnumerable<IChartRunner> runners)
{
    private readonly Dictionary<string, IChartRunner> _byName =
        runners.ToDictionary(r => r.Name, StringComparer.OrdinalIgnoreCase);

    public bool TryGetRunner(string queryName, out IChartRunner runner) =>
        _byName.TryGetValue(queryName, out runner!);
}
