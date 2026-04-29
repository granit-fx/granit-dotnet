namespace Granit.Analytics.Internal;

/// <summary>
/// Registry of <see cref="IQueryAggregateRunner"/>s keyed by query name. Mirrors
/// <c>MetricEndpointService.TryGetRunner</c> for the query-aggregate side: a
/// single point of resolution so the dashboard render path stays
/// reflection-free at request time.
/// </summary>
internal sealed class QueryAggregateService(IEnumerable<IQueryAggregateRunner> runners)
{
    private readonly Dictionary<string, IQueryAggregateRunner> _byName =
        runners.ToDictionary(r => r.Name, StringComparer.OrdinalIgnoreCase);

    public bool TryGetRunner(string queryName, out IQueryAggregateRunner runner) =>
        _byName.TryGetValue(queryName, out runner!);
}
