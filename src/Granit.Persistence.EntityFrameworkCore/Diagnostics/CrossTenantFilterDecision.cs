using Granit.MultiTenancy;
using Microsoft.Extensions.Logging;

namespace Granit.Persistence.EntityFrameworkCore.Diagnostics;

/// <summary>
/// Single source of truth for the "absent tenant context ⇒ fail-closed unless signaled"
/// decision shared by the CRUD path (<see cref="EfStoreBase{TEntity, TContext}"/>) and the
/// QueryEngine path (<c>TenantQueryScope</c>).
/// </summary>
/// <remarks>
/// <para>
/// When an <see cref="Granit.Domain.IMultiTenant"/> entity is queried and no tenant is active
/// (<see cref="ICurrentTenant.IsAvailable"/> is <c>false</c>), the multi-tenant named filter is
/// bypassed <b>only</b> if the request carries an explicit host-access signal
/// (<see cref="IHostAccessContext.IsHostAccess"/>, set via <c>.AllowHostAccess()</c>). Any
/// unsignaled absence of a tenant fails CLOSED: the filter stays active and only the host
/// partition (<c>TenantId == null</c>) is visible — a tenant-context loss can never widen a
/// query to every tenant.
/// </para>
/// <para>
/// Every branch records <c>granit.persistence.cross_tenant_query</c> and logs the companion
/// event, keeping metric origins (<c>host_endpoint</c> / <c>implicit_unsignaled</c>) and log
/// wording identical across both call sites.
/// </para>
/// </remarks>
internal static partial class CrossTenantFilterDecision
{
    /// <summary>
    /// Evaluates the tenant-filter decision for a multi-tenant entity whose tenant context is
    /// absent, emitting the metric and log for the chosen branch.
    /// </summary>
    /// <param name="entity">The CLR name of the queried entity (metric/log tag).</param>
    /// <param name="hostAccess">Host-access signal, or <c>null</c> when unavailable.</param>
    /// <param name="metrics">Persistence metrics sink, or <c>null</c>.</param>
    /// <param name="logger">Logger for the companion event.</param>
    /// <returns>
    /// <c>true</c> when the multi-tenant filter should be bypassed (signaled host access);
    /// <c>false</c> to fail closed (keep the filter, host partition only).
    /// </returns>
    public static bool ShouldBypassMultiTenantFilter(
        string entity,
        IHostAccessContext? hostAccess,
        PersistenceMetrics? metrics,
        ILogger logger)
    {
        // Only a signaled host-access route (.AllowHostAccess()) is authorized to read across
        // tenants. Any UNSIGNALED absence of a tenant is treated as a context loss and fails
        // CLOSED: the multi-tenant named filter is left in place, so the factory-created
        // GranitDbContext restricts the result to the host partition
        // (TenantId == CurrentTenantId == null) instead of leaking every tenant's rows.
        if (hostAccess?.IsHostAccess == true)
        {
            metrics?.RecordCrossTenantQuery(entity, "host_endpoint");
            LogHostEndpointCrossTenantQuery(logger, entity);
            return true;
        }

        metrics?.RecordCrossTenantQuery(entity, "implicit_unsignaled");
        LogUnsignaledCrossTenantQuery(logger, entity);
        return false;
    }

    [LoggerMessage(
        EventId = 8100,
        Level = LogLevel.Warning,
        Message =
            "Unsignaled cross-tenant access on {Entity}: ICurrentTenant.IsAvailable=false and "
            + "no host-access signal was set on the request. This signals a tenant-context "
            + "loss between request entry and the data layer. The query has been restricted to "
            + "the host partition (fail-closed) — no foreign-tenant rows are returned. Investigate "
            + "the call-site, or mark the endpoint with .AllowHostAccess() if cross-tenant access "
            + "is intended.")]
    public static partial void LogUnsignaledCrossTenantQuery(ILogger logger, string entity);

    [LoggerMessage(
        EventId = 8101,
        Level = LogLevel.Information,
        Message =
            "Host-endpoint cross-tenant query on {Entity}: served via .AllowHostAccess() route.")]
    public static partial void LogHostEndpointCrossTenantQuery(ILogger logger, string entity);
}
