using Granit.Auditing.Domain;
using Granit.Identity.Local.Auditing;

namespace Granit.Identity.Local.Endpoints.Internal;

/// <summary>
/// Builds the durable <see cref="AuditEntry"/> recorded for an administrator impersonation — a
/// <see cref="AuditCategory.PrivilegedAccess"/> entry (ISO 27001 A.12.4.3) whose synthetic change
/// carries the impersonated user as its <see cref="AuditEntityChange.EntityId"/> and the marker
/// <see cref="ImpersonationAuditMarker.AuditEntityType"/> as its entity type. The impersonator is the
/// entry's actor. The transparency-notification handler reads both back from the persisted entry.
/// </summary>
internal static class ImpersonationAuditEntry
{
    public static AuditEntry Create(
        DateTimeOffset timestamp,
        string impersonatorId,
        string? impersonatorName,
        Guid targetUserId,
        Guid? tenantId,
        string? ipAddress,
        string? userAgent,
        string? correlationId) =>
        new()
        {
            Timestamp = timestamp,
            UserId = impersonatorId,
            UserName = impersonatorName,
            Category = AuditCategory.PrivilegedAccess,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            TenantId = tenantId,
            CorrelationId = correlationId,
            EntityChanges =
            [
                new AuditEntityChange
                {
                    EntityType = ImpersonationAuditMarker.AuditEntityType,
                    EntityId = targetUserId.ToString(),
                    ChangeType = AuditChangeType.Created,
                    PropertyChanges =
                    [
                        new AuditPropertyChange { PropertyName = "Outcome", NewValue = "success" },
                    ],
                },
            ],
        };
}
