using Granit.MultiTenancy;
using Wolverine;

namespace Granit.Wolverine.Behaviors;

/// <summary>
/// Wolverine middleware that restores the current tenant context from incoming
/// message envelope headers in background handler threads.
/// </summary>
/// <remarks>
/// <para>
/// Reads the <c>X-Tenant-Id</c> header set by
/// <see cref="Granit.Wolverine.Middleware.OutgoingContextMiddleware"/> on the publisher side
/// and calls <see cref="ICurrentTenant.Change(Guid?, string?)"/> to activate the tenant
/// for the duration of the handler invocation.
/// </para>
/// <para>
/// Without this behavior, <c>ICurrentTenant.Id</c> is null in background handlers,
/// causing EF Core global query filters (<c>WHERE TenantId = X</c>) to be skipped —
/// a potential cross-tenant data leak.
/// </para>
/// <para>
/// If the header is absent or its value cannot be parsed as a <see cref="Guid"/>,
/// no exception is raised and the handler executes without a tenant context.
/// </para>
/// </remarks>
public sealed class TenantContextBehavior(ICurrentTenant currentTenant)
{
    private IDisposable? _scope;

    /// <summary>
    /// Activates the tenant from the <c>X-Tenant-Id</c> header before the handler runs.
    /// </summary>
    /// <param name="envelope">The incoming Wolverine envelope.</param>
    public void Before(Envelope envelope)
    {
        if (envelope.Headers.TryGetValue(
                Middleware.OutgoingContextMiddleware.TenantIdHeader, out string? tenantIdStr)
            && Guid.TryParse(tenantIdStr, out Guid tenantId))
        {
            _scope = currentTenant.Change(tenantId);
        }
    }

    /// <summary>Restores the previous tenant context after the handler completes.</summary>
    public void After() => _scope?.Dispose();
}
