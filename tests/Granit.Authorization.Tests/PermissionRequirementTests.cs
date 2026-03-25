using Granit.Authorization.Authorization;
using Microsoft.AspNetCore.Authorization;
using Shouldly;
using Xunit;

namespace Granit.Authorization.Tests;

public sealed class PermissionRequirementTests
{
    [Fact]
    public void Constructor_SetsPermissionName()
    {
        PermissionRequirement requirement = new("Invoices.Delete");

        requirement.PermissionName.ShouldBe("Invoices.Delete");
    }

    [Fact]
    public void ImplementsIAuthorizationRequirement()
    {
        PermissionRequirement requirement = new("Invoices.Read");

        requirement.ShouldBeAssignableTo<IAuthorizationRequirement>();
    }

    [Fact]
    public void Constructor_DifferentPermission_StoresCorrectValue()
    {
        PermissionRequirement requirement = new("BlobStorage.Administration.Read");

        requirement.PermissionName.ShouldBe("BlobStorage.Administration.Read");
    }
}
