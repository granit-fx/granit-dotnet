using Granit.Domain;
using Granit.Identity.Local.Domain;
using Shouldly;
using Xunit;

namespace Granit.Identity.Local.Tests.Entities;

public sealed class EntityTests
{
    [Fact]
    public void GranitUser_Default_Values()
    {
        LocalIdentity user = new();

        user.FirstName.ShouldBeNull();
        user.LastName.ShouldBeNull();
        user.TenantId.ShouldBeNull();
        user.IsDeleted.ShouldBeFalse();
        user.DeletedAt.ShouldBeNull();
        user.DeletedBy.ShouldBeNull();
        user.CustomAttributesJson.ShouldBeNull();
        user.CreatedAt.ShouldBe(default);
        user.CreatedBy.ShouldBe(string.Empty);
        user.ModifiedAt.ShouldBeNull();
        user.ModifiedBy.ShouldBeNull();
    }

    [Fact]
    public void GranitUser_Implements_IMultiTenant()
    {
        LocalIdentity user = new();

        user.ShouldBeAssignableTo<IMultiTenant>();
    }

    [Fact]
    public void GranitUser_IMultiTenant_TenantId()
    {
        var tenantId = Guid.NewGuid();
        LocalIdentity user = new() { TenantId = tenantId };

        user.TenantId.ShouldBe(tenantId);
    }

    [Fact]
    public void GranitRole_Default_Values()
    {
        GranitRole role = new();

        role.Description.ShouldBeNull();
    }

    [Fact]
    public void GranitRole_Description_Setter()
    {
        GranitRole role = new() { Description = "Administrator role" };

        role.Description.ShouldBe("Administrator role");
    }
}
