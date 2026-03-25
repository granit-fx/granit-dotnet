using Granit.AuditLog.Endpoints.Permissions;
using Shouldly;
using Xunit;

namespace Granit.AuditLog.Endpoints.Tests.Permissions;

public sealed class AuditLogPermissionsTests
{
    [Fact]
    public void GroupName_IsAuditLog() => AuditLogPermissions.GroupName.ShouldBe("AuditLog");

    [Fact]
    public void EntriesRead_FollowsThreeSegmentFormat()
    {
        AuditLogPermissions.Entries.Read.ShouldBe("AuditLog.Entries.Read");
        AuditLogPermissions.Entries.Read.Split('.').Length.ShouldBe(3);
    }

    [Fact]
    public void EntriesRead_StartsWithGroupName()
    {
        AuditLogPermissions.Entries.Read.ShouldStartWith(AuditLogPermissions.GroupName + ".");
    }
}
