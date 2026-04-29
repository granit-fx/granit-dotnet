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
        return Update(_descriptor with
        {
            RequiredPermission = permission,
            // RequirePermission satisfies the strict-config validator's
            // "either gated or explicitly anonymous" rule.
            AnonymousAccessAcknowledged = false,
        });
    }

    /// <summary>
    /// Explicit opt-in: this EntitySet IS anonymous-readable, by design.
    /// Use case: a public reference-data feed (countries, currencies) that
    /// truly has no per-tenant restriction. Without calling this method (or
    /// <see cref="RequirePermission"/>), <c>MapGranitODataEndpoints</c>
    /// throws at startup — the strict-config validator (C6 #1395) refuses
    /// to ship a permission-less EntitySet by accident.
    /// </summary>
    public ODataEntitySetBuilder<TEntity> AllowAnonymousAccess()
        => Update(_descriptor with
        {
            RequiredPermission = null,
            AnonymousAccessAcknowledged = true,
        });

    /// <summary>
    /// Caps the user-supplied <c>$top</c>. Requests above this value are
    /// silently clamped and the response carries an
    /// <c>OData-MaxTop-Applied</c> header so observability tools can spot
    /// misconfigured BI refresh jobs. Must be positive.
    /// </summary>
    public ODataEntitySetBuilder<TEntity> MaxTop(int maxTop)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxTop);
        return Update(_descriptor with { MaxTop = maxTop });
    }

    /// <summary>
    /// Sets the server-side default page size returned when the caller
    /// omits <c>$top</c>. The response then carries <c>@odata.nextLink</c>
    /// for OData clients to walk pagination. Must be positive.
    /// </summary>
    public ODataEntitySetBuilder<TEntity> PageSize(int pageSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageSize);
        return Update(_descriptor with { PageSize = pageSize });
    }

    /// <summary>
    /// Enables <c>$count=true</c> on this EntitySet. Disabled by default —
    /// huge tables would otherwise face a full-table-scan count on every BI
    /// refresh. Enable explicitly for sets where the count query is cheap
    /// (small tables, or covered by a dedicated index).
    /// </summary>
    public ODataEntitySetBuilder<TEntity> EnableCount()
        => Update(_descriptor with { CountEnabled = true });

    /// <summary>
    /// Whitelists the top-level navigation properties allowed in
    /// <c>$expand</c>. Default behaviour is <c>$expand</c> disabled —
    /// without this call, any <c>$expand</c> request returns
    /// <c>400 Bad Request</c>. An empty list still disables expand
    /// (explicit "I want zero navigations exposed").
    /// </summary>
    /// <param name="properties">Navigation property names allowed at the top level (e.g. <c>"Customer"</c>, <c>"Lines"</c>).</param>
    public ODataEntitySetBuilder<TEntity> ExpandWhitelist(params string[] properties)
    {
        ArgumentNullException.ThrowIfNull(properties);
        return Update(_descriptor with
        {
            ExpandWhitelist = [.. properties],
            ExpandConfigurationAcknowledged = true,
        });
    }

    /// <summary>
    /// Explicit opt-out: this EntitySet does NOT support <c>$expand</c> — any
    /// expand request returns <c>400 Bad Request</c>. Equivalent to calling
    /// <see cref="ExpandWhitelist"/> with no arguments, but reads more
    /// clearly at the call site. The strict-config validator (C6 #1395)
    /// requires either this or <see cref="ExpandWhitelist"/> on every
    /// EntitySet — implicit "expand disabled" is a security smell, the
    /// host must declare the intent.
    /// </summary>
    public ODataEntitySetBuilder<TEntity> DisableExpand()
        => Update(_descriptor with
        {
            ExpandWhitelist = [],
            ExpandConfigurationAcknowledged = true,
        });

    /// <summary>
    /// Sets the maximum nesting depth allowed for <c>$expand</c>. Default
    /// is <c>1</c> (flat expand only). Set to <c>2</c> or more when a
    /// specific consumer needs to walk a navigation chain
    /// (e.g. <c>Customer($expand=Address)</c>). Higher depths exponentially
    /// increase the risk of N+1 explosions — review carefully.
    /// </summary>
    public ODataEntitySetBuilder<TEntity> MaxExpansionDepth(int depth)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(depth);
        return Update(_descriptor with { MaxExpansionDepth = depth });
    }

    private ODataEntitySetBuilder<TEntity> Update(ODataEntitySetDescriptor updated)
    {
        _options.Replace(_descriptor, updated);
        _descriptor = updated;
        return this;
    }
}
