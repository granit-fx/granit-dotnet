using Granit.Authorization.Domain;
using Granit.Domain;
using Shouldly;
using Xunit;

namespace Granit.Authorization.EntityFrameworkCore.Tests;

public sealed class PermissionGrantEntityTests
{
    [Fact]
    public void InheritsFromAuditedEntity()
    {
        PermissionGrant grant = new();

        grant.ShouldBeAssignableTo<AuditedEntity>();
    }

    [Fact]
    public void ImplementsIMultiTenant()
    {
        PermissionGrant grant = new();

        grant.ShouldBeAssignableTo<IMultiTenant>();
    }

    [Fact]
    public void Name_Default_IsEmptyString()
    {
        PermissionGrant grant = new();

        grant.Name.ShouldBe(string.Empty);
    }

    [Fact]
    public void RoleName_Default_IsEmptyString()
    {
        PermissionGrant grant = new();

        grant.RoleName.ShouldBe(string.Empty);
    }

    [Fact]
    public void TenantId_Default_IsNull()
    {
        PermissionGrant grant = new();

        grant.TenantId.ShouldBeNull();
    }

    [Fact]
    public void Properties_CanBeSet()
    {
        var tenantId = Guid.NewGuid();
        var id = Guid.NewGuid();

        PermissionGrant grant = new()
        {
            Id = id,
            Name = "Invoices.Delete",
            RoleName = "accountant",
            TenantId = tenantId
        };

        grant.Id.ShouldBe(id);
        grant.Name.ShouldBe("Invoices.Delete");
        grant.RoleName.ShouldBe("accountant");
        grant.TenantId.ShouldBe(tenantId);
    }
}
