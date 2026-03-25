using Granit.Auditing.Endpoints.Permissions;
using Shouldly;
using Xunit;

namespace Granit.Auditing.Endpoints.Tests.Permissions;

public sealed class AuditingPermissionsTests
{
    [Fact]
    public void GroupName_IsAuditing() => AuditingPermissions.GroupName.ShouldBe("Auditing");

    [Fact]
    public void EntriesRead_FollowsThreeSegmentFormat()
    {
        AuditingPermissions.AuditEntries.Read.ShouldBe("Auditing.AuditEntries.Read");
        AuditingPermissions.AuditEntries.Read.Split('.').Length.ShouldBe(3);
    }

    [Fact]
    public void EntriesRead_StartsWithGroupName() =>
        AuditingPermissions.AuditEntries.Read.ShouldStartWith(AuditingPermissions.GroupName + ".");
}
