using Granit.MultiTenancy;
using Granit.Wolverine.Diagnostics;
using Wolverine;

namespace Granit.Wolverine.Behaviors;

/// <summary>
/// Wolverine middleware that restores the current tenant context from incoming
/// message envelope headers in background handler threads.
/// </summary>
/// <remarks>
/// <para>
/// Reads the <c>X-Tenant-Id</c> header set by
/// <see cref="Granit.Wolverine.Middleware.OutgoingContextMiddleware"/> on the publisher
/// side and calls <see cref="ICurrentTenant.Change(Guid?, string?, string?)"/> to activate the
/// tenant for the duration of the handler invocation.
/// </para>
/// <para>
/// Without this behavior, <c>ICurrentTenant.Id</c> is null in background handlers, so
/// EF Core multi-tenant query filters do not discriminate. Envelopes received without
/// a tenant header are recorded as <c>granit.wolverine.envelope.no_tenant</c>: an
/// observability signal — not an authorization gate. The actual gate is the
/// per-(user, tenant, action) permission check at the handler boundary; messages
/// legitimately running outside any tenant scope should mark the type with
/// <see cref="CrossTenantMessageAttribute"/> for clarity.
/// </para>
/// <para>
/// In single-tenant deployments (<c>Granit.MultiTenancy</c> not registered),
/// <see cref="ICurrentTenant"/> resolves to <c>NullTenantContext</c> and the
/// middleware short-circuits — no metric, no scope.
/// </para>
/// </remarks>
public sealed class TenantContextBehavior(
    ICurrentTenant currentTenant,
    WolverineMetrics metrics)
{
    private readonly ICurrentTenant _currentTenant = currentTenant;
    private readonly WolverineMetrics _metrics = metrics;
    private IDisposable? _scope;

    /// <summary>
    /// Activates the tenant from the <c>X-Tenant-Id</c> header before the handler runs.
    /// </summary>
    /// <param name="envelope">The incoming Wolverine envelope.</param>
    public void Before(Envelope envelope)
    {
        // Single-tenant / non-MT deployment: nothing to restore, no signal to record.
        if (_currentTenant is NullTenantContext)
        {
            return;
        }

        if (envelope.Headers.TryGetValue(
                Middleware.OutgoingContextMiddleware.TenantIdHeader, out string? tenantIdStr)
            && Guid.TryParse(tenantIdStr, out Guid tenantId))
        {
            _scope = _currentTenant.Change(tenantId);
            return;
        }

        // Envelope without tenant header — observability only.
        // Prefer the runtime CLR type when available; Wolverine's `envelope.MessageType`
        // uses a wire-format identifier that is not always resolvable across assemblies.
        Type? messageType = envelope.Message?.GetType()
            ?? (envelope.MessageType is not null ? Type.GetType(envelope.MessageType) : null);
        string messageTypeName = messageType?.Name
            ?? envelope.MessageType
            ?? "(unknown)";

        bool isCrossTenant = messageType is not null
            && Attribute.IsDefined(messageType, typeof(CrossTenantMessageAttribute), inherit: false);

        _metrics.RecordEnvelopeWithoutTenant(messageTypeName, isCrossTenant ? "marked" : "unmarked");
    }

    /// <summary>Restores the previous tenant context after the handler completes.</summary>
    public void After() => _scope?.Dispose();
}
