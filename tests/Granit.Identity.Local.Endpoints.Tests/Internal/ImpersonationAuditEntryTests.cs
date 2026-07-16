using Granit.Auditing.Domain;
using Granit.Identity.Local.Auditing;
using Granit.Identity.Local.Endpoints.Internal;
using Shouldly;
using Xunit;

namespace Granit.Identity.Local.Endpoints.Tests.Internal;

/// <summary>
/// The impersonation audit entry is the compliance source of truth and the shape the transparency
/// notification derives from: a <see cref="AuditCategory.PrivilegedAccess"/> entry whose synthetic
/// change carries the impersonated user as its <see cref="AuditEntityChange.EntityId"/> under the
/// <see cref="ImpersonationAuditMarker.AuditEntityType"/> marker, with the impersonator as the actor.
/// </summary>
public sealed class ImpersonationAuditEntryTests
{
    [Fact]
    public void Create_BuildsPrivilegedAccessEntry_WithTargetAsEntityIdAndImpersonatorAsActor()
    {
        var timestamp = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var targetUserId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        AuditEntry entry = ImpersonationAuditEntry.Create(
            timestamp,
            "admin-id",
            "Admin User",
            targetUserId,
            tenantId,
            "203.0.113.4",
            "curl/8",
            "trace-1");

        entry.Category.ShouldBe(AuditCategory.PrivilegedAccess);
        entry.Timestamp.ShouldBe(timestamp);
        entry.UserId.ShouldBe("admin-id");
        entry.UserName.ShouldBe("Admin User");
        entry.TenantId.ShouldBe(tenantId);
        entry.IpAddress.ShouldBe("203.0.113.4");
        entry.UserAgent.ShouldBe("curl/8");
        entry.CorrelationId.ShouldBe("trace-1");

        AuditEntityChange change = entry.EntityChanges.ShouldHaveSingleItem();
        change.EntityType.ShouldBe(ImpersonationAuditMarker.AuditEntityType);
        change.EntityId.ShouldBe(targetUserId.ToString());
        change.ChangeType.ShouldBe(AuditChangeType.Created);
    }
}
