using Granit.Domain;
using Granit.Identity.Local.Domain;
using Shouldly;
using Xunit;

namespace Granit.Identity.Local.Tests.Domain;

public sealed class UserGroupEntityTests
{
    [Fact]
    public void GranitUserGroup_Default_Values()
    {
        GranitUserGroup group = new();

        group.Name.ShouldBe(string.Empty);
        group.Description.ShouldBeNull();
        group.TenantId.ShouldBeNull();
    }

    [Fact]
    public void GranitUserGroup_Implements_IMultiTenant()
    {
        GranitUserGroup group = new();

        group.ShouldBeAssignableTo<IMultiTenant>();
    }

    [Fact]
    public void GranitUserGroupMember_Default_Values()
    {
        GranitUserGroupMember member = new();

        member.GroupId.ShouldBe(Guid.Empty);
        member.UserId.ShouldBe(Guid.Empty);
        member.TenantId.ShouldBeNull();
    }

    [Fact]
    public void GranitUserGroupMember_Implements_IMultiTenant()
    {
        GranitUserGroupMember member = new();

        member.ShouldBeAssignableTo<IMultiTenant>();
    }

    [Fact]
    public void GranitUserGroupMember_Property_Setters()
    {
        var groupId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        GranitUserGroupMember member = new()
        {
            GroupId = groupId,
            UserId = userId,
            TenantId = tenantId,
        };

        member.GroupId.ShouldBe(groupId);
        member.UserId.ShouldBe(userId);
        member.TenantId.ShouldBe(tenantId);
    }
}
