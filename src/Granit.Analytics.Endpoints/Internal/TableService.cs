namespace Granit.Analytics.Endpoints.Internal;

/// <summary>
/// Registry of <see cref="ITableRunner"/>s keyed by query name. Mirrors
/// <see cref="QueryAggregateService.TryGetRunner"/> for the table side: a
/// single point of resolution so the dashboard render path stays
/// reflection-free at request time.
/// </summary>
internal sealed class TableService(IEnumerable<ITableRunner> runners)
{
    private readonly Dictionary<string, ITableRunner> _byName =
        runners.ToDictionary(r => r.Name, StringComparer.OrdinalIgnoreCase);

    public bool TryGetRunner(string queryName, out ITableRunner runner) =>
        _byName.TryGetValue(queryName, out runner!);
}
