using Granit.Analytics.Internal;

namespace Granit.Analytics.EntityFrameworkCore.Internal;

/// <summary>
/// Registry of <see cref="IPivotRunner"/>s keyed by query name. Mirrors
/// <see cref="QueryAggregateService"/> / <see cref="TableService"/> /
/// <see cref="ChartService"/> for the pivot side: a single point of
/// resolution so the dashboard render path stays reflection-free at
/// request time.
/// </summary>
internal sealed class PivotService(IEnumerable<IPivotRunner> runners)
{
    private readonly Dictionary<string, IPivotRunner> _byName =
        runners.ToDictionary(r => r.Name, StringComparer.OrdinalIgnoreCase);

    public bool TryGetRunner(string queryName, out IPivotRunner runner) =>
        _byName.TryGetValue(queryName, out runner!);
}
