namespace Granit.Dashboards;

/// <summary>
/// Read-only registry of every <see cref="DashboardDefinition"/> registered across
/// all loaded modules. Surfaces the catalogue to the import endpoint
/// (story B4) and the admin import dialog (story B5).
/// </summary>
public interface IDashboardDefinitionRegistry
{
    /// <summary>
    /// Returns every registered definition, ordered by <see cref="DashboardCategory"/>
    /// then by <see cref="DashboardDefinition.Name"/> (ordinal). Stable ordering across
    /// processes — useful for tests and snapshot fixtures.
    /// </summary>
    IReadOnlyList<IDashboardDefinitionDescriptor> GetAll();

    /// <summary>
    /// Looks up a definition by its wire identifier. Returns <c>null</c> when the name
    /// is not registered (callers translate to a 404 at the HTTP layer).
    /// </summary>
    IDashboardDefinitionDescriptor? Find(string name);
}
