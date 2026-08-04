using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Granit.Persistence.EntityFrameworkCore.Diagnostics;

/// <summary>
/// Metrics for Granit.Persistence.EntityFrameworkCore interceptors and data operations.
/// </summary>
/// <remarks>
/// Meter name: <c>Granit.Persistence.EntityFrameworkCore</c>. All metric names follow the
/// <c>granit.persistence.{entity}.{action}</c> convention.
/// </remarks>
public sealed class PersistenceMetrics
{
    public const string MeterName = "Granit.Persistence.EntityFrameworkCore";

    private const string TagTenantId = "tenant_id";
    private const string DefaultTenant = "global";

    private const string TagEntity = "entity";
    private const string TagOrigin = "origin";

    private readonly Counter<long> _entitiesPurged;
    private readonly Counter<long> _crossTenantQueries;

    public PersistenceMetrics(IMeterFactory meterFactory)
    {
        Meter meter = meterFactory.Create(MeterName);

        _entitiesPurged = meter.CreateCounter<long>(
            "granit.persistence.entity.purged",
            description: "Number of soft-deleted entities permanently purged.");

        _crossTenantQueries = meter.CreateCounter<long>(
            "granit.persistence.cross_tenant_query",
            description:
                "Number of EfStoreBase queries that bypassed the multi-tenant filter. "
                + "Tagged with `entity` (CLR name), `origin` (`host_endpoint` = served "
                + "via a route marked .AllowHostAccess(); `explicit` = caller used "
                + "QueryAcrossTenants(); `implicit_unsignaled` = bypass without host-access "
                + "signal — alert-worthy, may indicate a tenant-context leak in flight) and "
                + "`tenant_id` (the active tenant at the call site, `global` when no tenant "
                + "context was available — always the case for the implicit origins).");
    }

    public void RecordEntitiesPurged(string? tenantId, int count) =>
        _entitiesPurged.Add(count, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
        });

    /// <summary>
    /// Records a query that bypassed the <c>MultiTenant</c> named filter.
    /// </summary>
    /// <param name="entityName">The CLR name of the queried entity.</param>
    /// <param name="origin">
    /// <c>"host_endpoint"</c> when the bypass is served via a route marked
    /// <c>.AllowHostAccess()</c> (signal observed on <c>HttpContext.Features</c>);
    /// <c>"explicit"</c> when the caller invoked
    /// <see cref="EfStoreBase{TEntity, TContext}.QueryAcrossTenants(TContext, string, string, int)"/>;
    /// <c>"implicit_unsignaled"</c> when the bypass occurred without any host-access
    /// signal — this is the alert-worthy origin that may indicate a tenant-context leak.
    /// </param>
    /// <param name="tenantId">
    /// The active tenant at the call site, or <c>null</c> when no tenant context is
    /// available (coalesced to <c>"global"</c>). The host/implicit origins fire precisely
    /// because the tenant is absent, so they always report <c>"global"</c>; the
    /// <c>explicit</c> origin reports the tenant whose scope issued the cross-tenant read.
    /// </param>
    public void RecordCrossTenantQuery(string entityName, string origin, string? tenantId = null) =>
        _crossTenantQueries.Add(1, new TagList
        {
            { TagEntity, entityName },
            { TagOrigin, origin },
            { TagTenantId, tenantId ?? DefaultTenant },
        });
}
