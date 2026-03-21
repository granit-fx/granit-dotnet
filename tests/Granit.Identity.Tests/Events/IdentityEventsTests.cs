using Granit.Identity.Events;
using Granit.Identity.Models;
using Shouldly;
using Xunit;

namespace Granit.Identity.Tests.Events;

public sealed class IdentityEventsTests
{
    [Fact]
    public void IdentityUserCreatedEto_SetsAllProperties()
    {
        var evt = new IdentityUserCreatedEto("u1", "alice", "alice@test.com");

        evt.UserId.ShouldBe("u1");
        evt.Username.ShouldBe("alice");
        evt.Email.ShouldBe("alice@test.com");
    }

    [Fact]
    public void IdentityUserCreatedEto_AllowsNullOptionalFields()
    {
        var evt = new IdentityUserCreatedEto("u1", null, null);

        evt.Username.ShouldBeNull();
        evt.Email.ShouldBeNull();
    }

    [Fact]
    public void IdentityUserProfileUpdatedEto_SetsAllProperties()
    {
        IdentityUserUpdate update = new("new@test.com", "Alice", "Doe");
        var evt = new IdentityUserProfileUpdatedEto("u1", update);

        evt.UserId.ShouldBe("u1");
        evt.Update.ShouldBe(update);
    }

    [Fact]
    public void IdentityUserEnabledChangedEto_SetsAllProperties()
    {
        var evt = new IdentityUserEnabledChangedEto("u1", true);

        evt.UserId.ShouldBe("u1");
        evt.Enabled.ShouldBeTrue();
    }

    [Fact]
    public void IdentityUserEnabledChangedEto_DisabledState()
    {
        var evt = new IdentityUserEnabledChangedEto("u1", false);

        evt.Enabled.ShouldBeFalse();
    }

    [Fact]
    public void IdentityRoleAssignedEto_SetsAllProperties()
    {
        var evt = new IdentityRoleAssignedEto("u1", "admin");

        evt.UserId.ShouldBe("u1");
        evt.RoleName.ShouldBe("admin");
    }

    [Fact]
    public void IdentityRoleRemovedEto_SetsAllProperties()
    {
        var evt = new IdentityRoleRemovedEto("u1", "editor");

        evt.UserId.ShouldBe("u1");
        evt.RoleName.ShouldBe("editor");
    }

    [Fact]
    public void IdentityGroupMembershipChangedEto_Added()
    {
        var evt = new IdentityGroupMembershipChangedEto("u1", "group-1", true);

        evt.UserId.ShouldBe("u1");
        evt.GroupId.ShouldBe("group-1");
        evt.Added.ShouldBeTrue();
    }

    [Fact]
    public void IdentityGroupMembershipChangedEto_Removed()
    {
        var evt = new IdentityGroupMembershipChangedEto("u1", "group-1", false);

        evt.Added.ShouldBeFalse();
    }

    [Fact]
    public void IdentityPasswordResetEto_SetsUserId()
    {
        var evt = new IdentityPasswordResetEto("u1");

        evt.UserId.ShouldBe("u1");
    }

    [Fact]
    public void IdentitySessionsRevokedEto_SetsUserId()
    {
        var evt = new IdentitySessionsRevokedEto("u1");

        evt.UserId.ShouldBe("u1");
    }

    [Fact]
    public void AllEvents_SupportRecordEquality()
    {
        var evt1 = new IdentityRoleAssignedEto("u1", "admin");
        var evt2 = new IdentityRoleAssignedEto("u1", "admin");

        evt1.ShouldBe(evt2);
    }

    [Fact]
    public void AllEvents_SupportRecordInequality()
    {
        var evt1 = new IdentityRoleAssignedEto("u1", "admin");
        var evt2 = new IdentityRoleAssignedEto("u1", "editor");

        evt1.ShouldNotBe(evt2);
    }
}
