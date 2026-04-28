namespace Granit.Dashboards.Internal;

internal sealed class DashboardDefinitionRegistry : IDashboardDefinitionRegistry
{
    private readonly IReadOnlyList<IDashboardDefinitionDescriptor> _ordered;
    private readonly Dictionary<string, IDashboardDefinitionDescriptor> _byName;

    public DashboardDefinitionRegistry(IEnumerable<IDashboardDefinitionDescriptor> descriptors)
    {
        ArgumentNullException.ThrowIfNull(descriptors);

        IDashboardDefinitionDescriptor[] snapshot = descriptors.ToArray();

        // Stable ordering: category index (declared enum order, broad-to-narrow), then
        // dashboard name. Ordinal comparison keeps test fixtures deterministic.
        _ordered = [.. snapshot
            .OrderBy(d => (int)d.Category)
            .ThenBy(d => d.Name, StringComparer.Ordinal)];

        _byName = snapshot.ToDictionary(d => d.Name, StringComparer.Ordinal);
    }

    public IReadOnlyList<IDashboardDefinitionDescriptor> GetAll() => _ordered;

    public IDashboardDefinitionDescriptor? Find(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        return _byName.TryGetValue(name, out IDashboardDefinitionDescriptor? d) ? d : null;
    }
}
