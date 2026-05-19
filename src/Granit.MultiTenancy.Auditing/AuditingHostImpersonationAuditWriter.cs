using System.Globalization;
using System.Security.Claims;
using Granit.Auditing;
using Granit.Auditing.Domain;
using Granit.MultiTenancy.Authorization;
using Granit.Timing;

namespace Granit.MultiTenancy.Auditing;

/// <summary>
/// <see cref="IHostImpersonationAuditWriter"/> implementation that persists every
/// gate decision via <see cref="IAuditingWriter"/>. Allowed decisions land in
/// <see cref="AuditCategory.PrivilegedAccess"/> (ISO 27001 A.12.4.3) and denied
/// decisions in <see cref="AuditCategory.AccessDenied"/> (A.12.4.1).
/// </summary>
/// <remarks>
/// Decision details (allowed flag, deny reason, resolver) are persisted as a
/// synthetic <see cref="AuditEntityChange"/> with <c>EntityType =
/// "HostImpersonation"</c> so investigators get a self-contained audit row
/// without needing to correlate with logs or metrics. The <c>CorrelationId</c>
/// still carries the request's trace id for cross-store lookups.
/// </remarks>
public sealed class AuditingHostImpersonationAuditWriter(IAuditingWriter auditingWriter, IClock clock)
    : IHostImpersonationAuditWriter
{
    private const string SyntheticEntityType = "HostImpersonation";
    private const string UnknownUserSentinel = "<unknown>";

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
                ?? UnknownUserSentinel,
            UserName = principal.FindFirst("name")?.Value
                ?? principal.Identity?.Name,
            Category = decision.Allowed
                ? AuditCategory.PrivilegedAccess
                : AuditCategory.AccessDenied,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            TenantId = targetTenantId,
            CorrelationId = correlationId,
            EntityChanges = [BuildDecisionChange(targetTenantId, decision, resolverType)],
        };

        await auditingWriter.WriteAsync(entry, cancellationToken).ConfigureAwait(false);
    }

    private static AuditEntityChange BuildDecisionChange(
        Guid targetTenantId,
        HostImpersonationDecision decision,
        string resolverType)
    {
        List<AuditPropertyChange> properties =
        [
            new AuditPropertyChange
            {
                PropertyName = "Allowed",
                NewValue = decision.Allowed ? "true" : "false",
            },
            new AuditPropertyChange
            {
                PropertyName = "ResolverType",
                NewValue = resolverType,
            },
        ];

        if (!string.IsNullOrEmpty(decision.DenyReasonCode))
        {
            properties.Add(new AuditPropertyChange
            {
                PropertyName = "DenyReasonCode",
                NewValue = decision.DenyReasonCode,
            });
        }

        return new AuditEntityChange
        {
            EntityType = SyntheticEntityType,
            EntityId = targetTenantId.ToString("D", CultureInfo.InvariantCulture),
            ChangeType = AuditChangeType.Created,
            PropertyChanges = properties,
        };
    }
}
