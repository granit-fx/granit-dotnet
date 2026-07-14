using Granit.Http.ODataExposure.Internal;
using Granit.QueryEngine;

namespace Granit.Http.ODataExposure.Options;

/// <summary>
/// Fluent registration surface for the <b>host-feed</b> mount — the
/// cross-tenant OData feed reserved for host operators (finance ops,
/// compliance, capacity planning) and gated by <c>MultiTenancySides.Host</c>
/// permissions.
/// </summary>
/// <remarks>
/// <para>
/// Distinct from <see cref="ODataExposureOptions"/> by design: the configure
/// callbacks of the two mounts cannot be confused at the call site, and the
/// host-feed builder forces the developer to write <c>IgnoreQueryFilters</c>
/// at the lambda level rather than flicking a flag.
/// </para>
/// <para>
/// The strict-config validator additions on this options surface (host-only
/// permissions, mandatory <c>AcknowledgeCrossTenantExposure</c> for
/// <c>IMultiTenant</c> entities, no anonymous access) are described on
/// <see cref="Extensions.ODataExposureEndpointRouteBuilderExtensions.MapGranitODataHostEndpoints"/>.
/// </para>
/// </remarks>
public sealed class ODataHostExposureOptions
{
    private readonly List<ODataEntitySetDescriptor> _descriptors = [];

    /// <summary>All host-feed EntitySets registered so far. Internal — consumed by the route builder once the configuration closure returns.</summary>
    internal IReadOnlyList<ODataEntitySetDescriptor> Descriptors => _descriptors;

    /// <summary>Permission gating the host-feed <c>$metadata</c> + service document. Consumed by the route builder. <see langword="null"/> until <see cref="RequireMetadataPermission"/> is called — the strict-config validator then refuses to start the host (#3005).</summary>
    internal string? MetadataPermission { get; private set; }

    /// <summary>
    /// Gates the host-feed <c>$metadata</c> and service-document routes
    /// behind the named permission — REQUIRED. The host-feed schema exposes
    /// the cross-tenant BI surface (EntitySet names, columns, navigations);
    /// there is deliberately no anonymous variant on this options surface.
    /// The permission MUST resolve to a definition with
    /// <c>MultiTenancySides.Host</c>, exactly like the per-set entity
    /// permissions — <c>Tenant</c> and <c>Both</c> are rejected at startup.
    /// Convention: <c>"OData.Host.{Module}.Metadata.Read"</c>.
    /// </summary>
    /// <param name="permission">Permission name checked by <c>IPermissionChecker</c> on every host-feed <c>$metadata</c> / service-document request.</param>
    public ODataHostExposureOptions RequireMetadataPermission(string permission)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);
        MetadataPermission = permission;
        return this;
    }

    /// <summary>
    /// Registers a host-feed EntitySet backed by <typeparamref name="TQueryDefinition"/>.
    /// </summary>
    /// <typeparam name="TEntity">Entity type — appears as an EntityType in the host-feed EDM model.</typeparam>
    /// <typeparam name="TQueryDefinition">The <c>QueryDefinition&lt;TEntity&gt;</c> shipped by the owning module — resolved through DI at request time.</typeparam>
    /// <param name="entitySetName">OData EntitySet name and route segment (PascalCase plural noun).</param>
    /// <returns>A host-feed builder for chaining further configuration on this set.</returns>
    /// <exception cref="ArgumentException"><paramref name="entitySetName"/> is empty or already registered in this options instance.</exception>
    public ODataHostEntitySetBuilder<TEntity> EntitySet<TEntity, TQueryDefinition>(string entitySetName)
        where TEntity : class
        where TQueryDefinition : QueryDefinition<TEntity>
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entitySetName);

        if (_descriptors.Any(d => string.Equals(d.EntitySetName, entitySetName, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException(
                $"Host-feed EntitySet '{entitySetName}' is already registered.",
                nameof(entitySetName));
        }

        ODataEntitySetDescriptor descriptor = new(
            EntitySetName: entitySetName,
            EntityType: typeof(TEntity),
            QueryDefinitionType: typeof(TQueryDefinition),
            RequiredPermission: null,
            FeedKind: ODataFeedKind.Host);

        _descriptors.Add(descriptor);
        return new ODataHostEntitySetBuilder<TEntity>(this, descriptor);
    }

    /// <summary>Replaces a descriptor in-place. Used by <see cref="ODataHostEntitySetBuilder{TEntity}"/> for fluent updates.</summary>
    internal void Replace(ODataEntitySetDescriptor old, ODataEntitySetDescriptor updated)
    {
        int index = _descriptors.IndexOf(old);
        if (index < 0)
        {
            throw new InvalidOperationException(
                $"Cannot replace descriptor for host-feed EntitySet '{old.EntitySetName}' — original instance not tracked.");
        }
        _descriptors[index] = updated;
    }
}

/// <summary>
/// Fluent builder for a host-feed EntitySet. Mirrors most of
/// <see cref="ODataEntitySetBuilder{TEntity}"/>'s surface, with two
/// host-specific changes: <c>AllowAnonymousAccess</c> is intentionally
/// absent (host-feed access is always gated), and
/// <see cref="AcknowledgeCrossTenantExposure"/> is mandatory for
/// <c>IMultiTenant</c> entities.
/// </summary>
public sealed class ODataHostEntitySetBuilder<TEntity>
    where TEntity : class
{
    private readonly ODataHostExposureOptions _options;
    private ODataEntitySetDescriptor _descriptor;

    internal ODataHostEntitySetBuilder(ODataHostExposureOptions options, ODataEntitySetDescriptor descriptor)
    {
        _options = options;
        _descriptor = descriptor;
    }

    /// <summary>
    /// Gates this host-feed EntitySet behind the named permission. The
    /// permission MUST resolve to a definition with
    /// <c>MultiTenancySides.Host</c>; the strict-config validator rejects
    /// <c>Tenant</c> or <c>Both</c> at <c>MapGranitODataHostEndpoints</c>
    /// time. Convention: <c>"OData.Host.{Module}.{Entity}.Read"</c>.
    /// </summary>
    public ODataHostEntitySetBuilder<TEntity> RequirePermission(string permission)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);
        return Update(_descriptor with { RequiredPermission = permission });
    }

    /// <summary>
    /// Acknowledges that this set returns data across tenants and supplies
    /// the per-query filter bypass. The host writes the bypass at the call
    /// site so the explicit "I know what I'm doing" lives in actual code,
    /// not in a flag — and the OData module stays free of the EF Core
    /// dependency. Typical usage:
    /// <code>
    /// .AcknowledgeCrossTenantExposure(q =&gt; q.IgnoreQueryFilters([GranitFilterNames.MultiTenant]))
    /// </code>
    /// Without this call, an <c>IMultiTenant</c> EntitySet on the host-feed
    /// throws at startup. Without the bypass lambda, the framework filter
    /// <c>tenantId == currentTenant.Id</c> returns no rows for a tenantless
    /// caller — fail-closed.
    /// </summary>
    /// <param name="bypass">Per-query transform applied to the queryable BEFORE the QueryEngine pipeline runs. Typed <c>Func&lt;IQueryable&lt;TEntity&gt;, IQueryable&lt;TEntity&gt;&gt;</c>.</param>
    public ODataHostEntitySetBuilder<TEntity> AcknowledgeCrossTenantExposure(
        Func<IQueryable<TEntity>, IQueryable<TEntity>> bypass)
    {
        ArgumentNullException.ThrowIfNull(bypass);
        return Update(_descriptor with
        {
            CrossTenantBypass = bypass,
            CrossTenantExposureAcknowledged = true,
        });
    }

    /// <summary>Caps the user-supplied <c>$top</c>. See <see cref="ODataEntitySetBuilder{TEntity}.MaxTop"/>.</summary>
    public ODataHostEntitySetBuilder<TEntity> MaxTop(int maxTop)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxTop);
        return Update(_descriptor with { MaxTop = maxTop });
    }

    /// <summary>Sets the server-side default page size returned when the caller omits <c>$top</c>. See <see cref="ODataEntitySetBuilder{TEntity}.PageSize"/>.</summary>
    public ODataHostEntitySetBuilder<TEntity> PageSize(int pageSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageSize);
        return Update(_descriptor with { PageSize = pageSize });
    }

    /// <summary>Enables <c>$count=true</c>. See <see cref="ODataEntitySetBuilder{TEntity}.EnableCount"/>.</summary>
    public ODataHostEntitySetBuilder<TEntity> EnableCount()
        => Update(_descriptor with { CountEnabled = true });

    /// <summary>Whitelists dotted navigation paths for <c>$expand</c>. See <see cref="ODataEntitySetBuilder{TEntity}.ExpandWhitelist"/>.</summary>
    public ODataHostEntitySetBuilder<TEntity> ExpandWhitelist(params string[] properties)
    {
        ArgumentNullException.ThrowIfNull(properties);
        return Update(_descriptor with
        {
            ExpandWhitelist = [.. properties],
            ExpandConfigurationAcknowledged = true,
        });
    }

    /// <summary>Explicitly disables <c>$expand</c> on this set. See <see cref="ODataEntitySetBuilder{TEntity}.DisableExpand"/>.</summary>
    public ODataHostEntitySetBuilder<TEntity> DisableExpand()
        => Update(_descriptor with
        {
            ExpandWhitelist = [],
            ExpandConfigurationAcknowledged = true,
        });

    /// <summary>Sets the maximum nesting depth for <c>$expand</c>. See <see cref="ODataEntitySetBuilder{TEntity}.MaxExpansionDepth"/>.</summary>
    public ODataHostEntitySetBuilder<TEntity> MaxExpansionDepth(int depth)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(depth);
        return Update(_descriptor with { MaxExpansionDepth = depth });
    }

    private ODataHostEntitySetBuilder<TEntity> Update(ODataEntitySetDescriptor updated)
    {
        _options.Replace(_descriptor, updated);
        _descriptor = updated;
        return this;
    }
}
