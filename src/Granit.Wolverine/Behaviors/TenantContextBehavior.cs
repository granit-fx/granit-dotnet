using Granit.MultiTenancy;
using Granit.Wolverine.Diagnostics;
using Granit.Wolverine.Options;
using Microsoft.Extensions.Options;
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
/// a potential cross-tenant data leak. Envelopes received without a tenant header
/// are recorded as <c>granit.wolverine.envelope.no_tenant</c>; when
/// <see cref="WolverineMessagingOptions.RequireEnvelopeTenant"/> is enabled, they
/// are rejected with an <see cref="InvalidOperationException"/> unless the
/// message type is annotated with <see cref="CrossTenantMessageAttribute"/>.
/// </para>
/// </remarks>
public sealed class TenantContextBehavior(
    ICurrentTenant currentTenant,
    WolverineMetrics metrics,
    IOptions<WolverineMessagingOptions> options)
{
    private readonly ICurrentTenant _currentTenant = currentTenant;
    private readonly WolverineMetrics _metrics = metrics;
    private readonly WolverineMessagingOptions _options = options.Value;
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
            _scope = _currentTenant.Change(tenantId);
            return;
        }

        // Envelope arrived without a tenant header. Always record the metric so
        // operators can quantify the migration backlog before flipping the gate.
        // Prefer the runtime CLR type when available — Wolverine's
        // `envelope.MessageType` uses a wire-format identifier that is not always
        // resolvable via `Type.GetType` across assemblies.
        Type? messageType = envelope.Message?.GetType()
            ?? (envelope.MessageType is not null ? Type.GetType(envelope.MessageType) : null);
        string messageTypeName = messageType?.Name
            ?? envelope.MessageType
            ?? "(unknown)";

        bool isCrossTenant = messageType is not null
            && Attribute.IsDefined(messageType, typeof(CrossTenantMessageAttribute), inherit: false);

        if (isCrossTenant)
        {
            _metrics.RecordEnvelopeWithoutTenant(messageTypeName, "allowed_marked");
            return;
        }

        if (_options.RequireEnvelopeTenant)
        {
            _metrics.RecordEnvelopeWithoutTenant(messageTypeName, "rejected");
            throw new InvalidOperationException(
                $"Envelope for message type '{messageTypeName}' lacks an X-Tenant-Id header. "
                + "RequireEnvelopeTenant is enabled and the message is not marked "
                + $"[{nameof(CrossTenantMessageAttribute)}]. Either ensure the publisher "
                + "propagates the tenant context, or annotate the message type as host-scope.");
        }

        _metrics.RecordEnvelopeWithoutTenant(messageTypeName, "allowed");
    }

    /// <summary>Restores the previous tenant context after the handler completes.</summary>
    public void After() => _scope?.Dispose();
}
