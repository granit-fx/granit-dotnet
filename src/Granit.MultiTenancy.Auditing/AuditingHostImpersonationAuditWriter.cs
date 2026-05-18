using System.Security.Claims;
using Granit.Auditing;
using Granit.Auditing.Domain;
using Granit.MultiTenancy.Authorization;
using Granit.Timing;

namespace Granit.MultiTenancy.Auditing;

/// <summary>
/// <see cref="IHostImpersonationAuditWriter"/> implementation that persists every
/// gate decision (allowed + denied) as a <see cref="AuditCategory.AccessDenied"/>
/// audit entry via <see cref="IAuditingWriter"/>.
/// </summary>
/// <remarks>
/// Even allowed impersonations are recorded under <c>AccessDenied</c>: from the
/// RSSI's point of view they are privileged-access events worth keeping in the
/// same retention bucket as denials. The <c>CorrelationId</c> carries the request's
/// trace id so the audit row links back to OTEL traces.
/// </remarks>
public sealed class AuditingHostImpersonationAuditWriter(IAuditingWriter auditingWriter, IClock clock)
    : IHostImpersonationAuditWriter
{
    public async ValueTask WriteAsync(
        ClaimsPrincipal principal,
        Guid targetTenantId,
        HostImpersonationDecision decision,
        string resolverType,
        string? ipAddress,
        string? userAgent,
        string? correlationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(principal);
        ArgumentNullException.ThrowIfNull(decision);

        AuditEntry entry = new()
        {
            Timestamp = clock.Now,
            UserId = principal.FindFirst("sub")?.Value
                ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? "unknown",
            UserName = principal.FindFirst("name")?.Value
                ?? principal.Identity?.Name,
            Category = AuditCategory.AccessDenied,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            TenantId = targetTenantId,
            CorrelationId = correlationId,
        };

        // No EntityChanges — host impersonation is not an entity mutation event.
        // The actor + tenant + decision is what the auditor cares about; further
        // detail (allowed vs denied, reason, resolver) lives in structured logs
        // and the OTEL counter, both keyed by the same CorrelationId.
        // Trailing context emitted via logger:
        // - decision.Allowed = {true|false}
        // - decision.DenyReasonCode = {NotConfigured|PermissionDenied|...}
        // - resolverType (which non-JWT resolver matched)

        await auditingWriter.WriteAsync(entry, cancellationToken).ConfigureAwait(false);
    }
}
