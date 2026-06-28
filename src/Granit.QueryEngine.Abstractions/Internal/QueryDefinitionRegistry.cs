namespace Granit.QueryEngine.Internal;

/// <summary>
/// Default <see cref="IQueryDefinitionRegistry"/> backed by the <see cref="IQueryDefinitionDescriptor"/>
/// instances registered in DI by <c>AddQueryDefinition</c>. Mirrors
/// <c>DashboardDefinitionRegistry</c>: a stable-ordered snapshot plus a by-name index.
/// </summary>
internal sealed class QueryDefinitionRegistry : IQueryDefinitionRegistry
{
    private readonly IReadOnlyList<IQueryDefinitionDescriptor> _ordered;
    private readonly Dictionary<string, IQueryDefinitionDescriptor> _byName;

    public QueryDefinitionRegistry(IEnumerable<IQueryDefinitionDescriptor> descriptors)
    {
        ArgumentNullException.ThrowIfNull(descriptors);

        IQueryDefinitionDescriptor[] snapshot = descriptors.ToArray();

        // Stable ordering by module, then by name. Ordinal comparison keeps test fixtures
        // deterministic and matches the wire identifier's culture-invariant nature.
        _ordered =
        [
            .. snapshot
                .OrderBy(d => d.ModuleName, StringComparer.Ordinal)
                .ThenBy(d => d.Name, StringComparer.Ordinal),
        ];

        _byName = snapshot.ToDictionary(d => d.Name, StringComparer.Ordinal);
    }

    public IReadOnlyList<IQueryDefinitionDescriptor> GetAll() => _ordered;

    public IQueryDefinitionDescriptor? Find(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        return _byName.TryGetValue(name, out IQueryDefinitionDescriptor? d) ? d : null;
    }
}
