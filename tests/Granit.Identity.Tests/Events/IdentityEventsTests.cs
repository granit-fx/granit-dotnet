using Granit.Identity.Events;
using Granit.Identity.Models;
using Shouldly;
using Xunit;

namespace Granit.Identity.Tests.Events;

public sealed class IdentityEventsTests
{
    [Fact]
    public void IdentityUserCreatedEvent_SetsAllProperties()
    {
        var evt = new IdentityUserCreatedEvent("u1", "alice", "alice@test.com");

        evt.UserId.ShouldBe("u1");
        evt.Username.ShouldBe("alice");
        evt.Email.ShouldBe("alice@test.com");
    }

    [Fact]
    public void IdentityUserCreatedEvent_AllowsNullOptionalFields()
    {
        var evt = new IdentityUserCreatedEvent("u1", null, null);

        evt.Username.ShouldBeNull();
        evt.Email.ShouldBeNull();
    }

    [Fact]
    public void IdentityUserProfileUpdatedEvent_SetsAllProperties()
    {
        IdentityUserUpdate update = new("new@test.com", "Alice", "Doe");
        var evt = new IdentityUserProfileUpdatedEvent("u1", update);

        evt.UserId.ShouldBe("u1");
        evt.Update.ShouldBe(update);
    }

    [Fact]
    public void IdentityUserEnabledChangedEvent_SetsAllProperties()
    {
        var evt = new IdentityUserEnabledChangedEvent("u1", true);

        evt.UserId.ShouldBe("u1");
        evt.Enabled.ShouldBeTrue();
    }

    [Fact]
    public void IdentityUserEnabledChangedEvent_DisabledState()
    {
        var evt = new IdentityUserEnabledChangedEvent("u1", false);

        evt.Enabled.ShouldBeFalse();
    }

    [Fact]
    public void IdentityRoleAssignedEvent_SetsAllProperties()
    {
        var evt = new IdentityRoleAssignedEvent("u1", "admin");

        evt.UserId.ShouldBe("u1");
        evt.RoleName.ShouldBe("admin");
    }

    [Fact]
    public void IdentityRoleRemovedEvent_SetsAllProperties()
    {
        var evt = new IdentityRoleRemovedEvent("u1", "editor");

        evt.UserId.ShouldBe("u1");
        evt.RoleName.ShouldBe("editor");
    }

    [Fact]
    public void IdentityGroupMembershipChangedEvent_Added()
    {
        var evt = new IdentityGroupMembershipChangedEvent("u1", "group-1", true);

        evt.UserId.ShouldBe("u1");
        evt.GroupId.ShouldBe("group-1");
        evt.Added.ShouldBeTrue();
    }

    [Fact]
    public void IdentityGroupMembershipChangedEvent_Removed()
    {
        var evt = new IdentityGroupMembershipChangedEvent("u1", "group-1", false);

        evt.Added.ShouldBeFalse();
    }

    [Fact]
    public void IdentityPasswordResetEvent_SetsUserId()
    {
        var evt = new IdentityPasswordResetEvent("u1");

        evt.UserId.ShouldBe("u1");
    }

    [Fact]
    public void IdentitySessionsRevokedEvent_SetsUserId()
    {
        var evt = new IdentitySessionsRevokedEvent("u1");

        evt.UserId.ShouldBe("u1");
    }

    [Fact]
    public void AllEvents_SupportRecordEquality()
    {
        var evt1 = new IdentityRoleAssignedEvent("u1", "admin");
        var evt2 = new IdentityRoleAssignedEvent("u1", "admin");

        evt1.ShouldBe(evt2);
    }

    [Fact]
    public void AllEvents_SupportRecordInequality()
    {
        var evt1 = new IdentityRoleAssignedEvent("u1", "admin");
        var evt2 = new IdentityRoleAssignedEvent("u1", "editor");

        evt1.ShouldNotBe(evt2);
    }
}
