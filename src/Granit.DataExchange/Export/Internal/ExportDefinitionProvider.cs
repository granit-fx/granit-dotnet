using Microsoft.Extensions.DependencyInjection;

namespace Granit.DataExchange.Export.Internal;

/// <summary>
/// Default <see cref="IExportDefinitionProvider"/> implementation.
/// Merges explicit <see cref="IExportDefinitionDescriptor"/> singletons from DI
/// with auto-generated <see cref="ReflectionExportDefinition"/> instances from
/// <see cref="IAutoExportDefinitionSource"/> providers.
/// </summary>
/// <remarks>
/// <para>
/// Resolution priority: explicit definitions always win. Auto-generated definitions
/// are only used for entity types that have no explicit definition registered.
/// </para>
/// <para>
/// Auto-generated definitions are built lazily on first access and cached for
/// the lifetime of the scope (this service is scoped because it resolves
/// <c>IExportDefinitionDescriptor</c> singletons from the service provider).
/// </para>
/// </remarks>
internal sealed class ExportDefinitionProvider(
    IServiceProvider serviceProvider,
    IEnumerable<IAutoExportDefinitionSource> sources) : IExportDefinitionProvider
{
    private IReadOnlyList<IExportDefinitionDescriptor>? _allDefinitions;

    /// <inheritdoc/>
    public IExportDefinitionDescriptor? FindByName(string definitionName)
    {
        // Check explicit definitions first (they always win)
        IExportDefinitionDescriptor? explicitMatch = serviceProvider
            .GetServices<IExportDefinitionDescriptor>()
            .FirstOrDefault(d => string.Equals(d.Name, definitionName, StringComparison.OrdinalIgnoreCase));

        if (explicitMatch is not null)
        {
            return explicitMatch;
        }

        // Fall back to auto-generated definitions
        return GetAutoDefinitions()
            .FirstOrDefault(d => string.Equals(d.Name, definitionName, StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc/>
    public IReadOnlyList<IExportDefinitionDescriptor> GetAll()
    {
        if (_allDefinitions is not null)
        {
            return _allDefinitions;
        }

        var explicits = serviceProvider
            .GetServices<IExportDefinitionDescriptor>()
            .ToList();

        // Collect entity types that already have explicit definitions
        var explicitEntityTypes = explicits
            .Select(d => d.EntityType)
            .ToHashSet();

        // Add auto-generated definitions only for entity types without explicit ones
        List<IExportDefinitionDescriptor> all = [.. explicits];
        foreach (ReflectionExportDefinition auto in GetAutoDefinitions())
        {
            if (!explicitEntityTypes.Contains(auto.EntityType))
            {
                all.Add(auto);
            }
        }

        _allDefinitions = all.AsReadOnly();
        return _allDefinitions;
    }

    private List<ReflectionExportDefinition> GetAutoDefinitions()
    {
        List<ReflectionExportDefinition> autoDefinitions = [];
        foreach (IAutoExportDefinitionSource source in sources)
        {
            foreach (Type entityType in source.GetEntityTypes())
            {
                autoDefinitions.Add(new ReflectionExportDefinition(entityType));
            }
        }

        return autoDefinitions;
    }
}
