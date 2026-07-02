namespace Granit.QueryEngine;

/// <summary>
/// Marker interface for query definition discovery via DI.
/// </summary>
/// <seealso cref="QueryDefinition{TEntity}"/>
public interface IQueryDefinitionDescriptor
{
    /// <summary>
    /// Unique name identifying this query definition (e.g. <c>"Acme.Patients"</c>).
    /// </summary>
    string Name { get; }

    /// <summary>
    /// The entity type this definition applies to.
    /// </summary>
    Type EntityType { get; }

    /// <summary>
    /// Owning module of this query (e.g. <c>"Auditing"</c> for both <c>AuditEntryQuery</c> and
    /// <c>AuditEntityChangeQuery</c>). Used to group and sort the query catalogue — entries are
    /// ordered by module, then by name. Defaults to <see cref="ModuleOf"/> of
    /// <see cref="EntityType"/>; override to customise.
    /// </summary>
    string ModuleName { get; }

    /// <summary>
    /// Derives the owning module name from an entity type: its assembly's simple name with the
    /// framework <c>Granit.</c> prefix stripped (so <c>Granit.Auditing</c> → <c>Auditing</c> and
    /// <c>Granit.Identity.Local</c> → <c>Identity.Local</c>). A non-framework assembly name is
    /// returned unchanged, so a consumer's queries group under their own assembly name.
    /// </summary>
    /// <remarks>
    /// A single trailing layer suffix (<c>.Abstractions</c>, <c>.EntityFrameworkCore</c>,
    /// <c>.Endpoints</c>) is stripped as well: an entity may legitimately live in a layer
    /// project — contracts shared via <c>.Abstractions</c> (so <c>Granit.Auditing.Abstractions</c>
    /// → <c>Auditing</c>), or a persisted row co-located with a shared DbContext in
    /// <c>.EntityFrameworkCore</c> — and the layer suffix is never the logical owning module.
    /// A definition whose entity assembly cannot express its real module (e.g. a feature-scoped
    /// table hosted in a parent module's DbContext) should override <see cref="ModuleName"/>.
    /// </remarks>
    static string ModuleOf(Type entityType)
    {
        ArgumentNullException.ThrowIfNull(entityType);

        string assembly = entityType.Assembly.GetName().Name ?? entityType.Namespace ?? "Unknown";
        const string prefix = "Granit.";
        if (!assembly.StartsWith(prefix, StringComparison.Ordinal))
        {
            return assembly;
        }

        string module = assembly[prefix.Length..];
        foreach (string suffix in (ReadOnlySpan<string>)[".Abstractions", ".EntityFrameworkCore", ".Endpoints"])
        {
            if (module.Length > suffix.Length && module.EndsWith(suffix, StringComparison.Ordinal))
            {
                return module[..^suffix.Length];
            }
        }

        return module;
    }
}
