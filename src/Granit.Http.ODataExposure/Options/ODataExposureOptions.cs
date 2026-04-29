using Granit.Http.ODataExposure.Internal;
using Granit.QueryEngine;

namespace Granit.Http.ODataExposure.Options;

/// <summary>
/// Fluent registration surface for OData EntitySets. The host code that
/// invokes <c>endpoints.MapGranitODataEndpoints(prefix, opts =&gt; ...)</c>
/// receives this object and declares one EntitySet per
/// <c>QueryDefinition&lt;TEntity&gt;</c> it wants exposed to BI tools.
/// </summary>
/// <remarks>
/// <para>
/// Order of operations: the closure runs ONCE at <c>Map*</c> time, the EDM
/// model is built from every <see cref="EntitySet{TEntity, TQueryDefinition}"/>
/// call, then per-set minimal-API endpoints are registered. Adding a
/// definition after the closure returns has no effect on routing.
/// </para>
/// </remarks>
public sealed class ODataExposureOptions
{
    private readonly List<ODataEntitySetDescriptor> _descriptors = [];

    /// <summary>
    /// All EntitySets registered so far. Internal — consumed by the route
    /// builder once the configuration closure returns.
    /// </summary>
    internal IReadOnlyList<ODataEntitySetDescriptor> Descriptors => _descriptors;

    /// <summary>
    /// Registers an EntitySet backed by <typeparamref name="TQueryDefinition"/>.
    /// </summary>
    /// <typeparam name="TEntity">The entity type — appears as an EntityType in the EDM model.</typeparam>
    /// <typeparam name="TQueryDefinition">The <c>QueryDefinition&lt;TEntity&gt;</c> shipped by the owning module — resolved through DI at request time so the framework's filter pipeline applies.</typeparam>
    /// <param name="entitySetName">OData EntitySet name and route segment. Convention: PascalCase plural noun (<c>"Invoices"</c>, <c>"Customers"</c>).</param>
    /// <returns>A builder for chaining further configuration on this set.</returns>
    /// <exception cref="ArgumentException"><paramref name="entitySetName"/> is empty or already registered in this options instance.</exception>
    public ODataEntitySetBuilder<TEntity> EntitySet<TEntity, TQueryDefinition>(string entitySetName)
        where TEntity : class
        where TQueryDefinition : QueryDefinition<TEntity>
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entitySetName);

        if (_descriptors.Any(d => string.Equals(d.EntitySetName, entitySetName, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException(
                $"EntitySet '{entitySetName}' is already registered.",
                nameof(entitySetName));
        }

        ODataEntitySetDescriptor descriptor = new(
            EntitySetName: entitySetName,
            EntityType: typeof(TEntity),
            QueryDefinitionType: typeof(TQueryDefinition),
            RequiredPermission: null);

        _descriptors.Add(descriptor);
        return new ODataEntitySetBuilder<TEntity>(this, descriptor);
    }

    /// <summary>Replaces a descriptor in-place. Used by <see cref="ODataEntitySetBuilder{TEntity}"/> for fluent updates.</summary>
    internal void Replace(ODataEntitySetDescriptor old, ODataEntitySetDescriptor updated)
    {
        int index = _descriptors.IndexOf(old);
        if (index < 0)
        {
            throw new InvalidOperationException(
                $"Cannot replace descriptor for EntitySet '{old.EntitySetName}' — original instance not tracked.");
        }
        _descriptors[index] = updated;
    }
}

/// <summary>Fluent builder returned by <see cref="ODataExposureOptions.EntitySet{TEntity, TQueryDefinition}"/>.</summary>
public sealed class ODataEntitySetBuilder<TEntity>
    where TEntity : class
{
    private readonly ODataExposureOptions _options;
    private ODataEntitySetDescriptor _descriptor;

    internal ODataEntitySetBuilder(ODataExposureOptions options, ODataEntitySetDescriptor descriptor)
    {
        _options = options;
        _descriptor = descriptor;
    }

    /// <summary>
    /// Gates this EntitySet behind the named permission. The permission is
    /// checked by the route's authorization pipeline before the OData query
    /// runs. Convention: <c>"OData.{Module}.{Entity}.Read"</c> per CLAUDE.md.
    /// </summary>
    public ODataEntitySetBuilder<TEntity> RequirePermission(string permission)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);

        ODataEntitySetDescriptor updated = _descriptor with { RequiredPermission = permission };
        _options.Replace(_descriptor, updated);
        _descriptor = updated;
        return this;
    }
}
