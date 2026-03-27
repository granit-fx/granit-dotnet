using Granit.Authorization.Attributes;
using Microsoft.AspNetCore.Authorization;
using Shouldly;
using Xunit;

namespace Granit.Authorization.Tests;

public sealed class PermissionAttributeTests
{
    [Fact]
    public void Constructor_SetsPolicy_ToPermissionName()
    {
        PermissionAttribute attr = new("Invoices.Delete");

        attr.Policy.ShouldBe("Invoices.Delete");
    }

    [Fact]
    public void InheritsFromAuthorizeAttribute()
    {
        PermissionAttribute attr = new("Invoices.Read");

        attr.ShouldBeAssignableTo<AuthorizeAttribute>();
    }

    [Fact]
    public void AllowsMultipleOnSameTarget()
    {
        AttributeUsageAttribute? usage = typeof(PermissionAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .FirstOrDefault();

        usage.ShouldNotBeNull();
        usage!.AllowMultiple.ShouldBeTrue();
    }

    // =========================================================================
    // AttributeUsage — valid targets
    // =========================================================================

    [Fact]
    public void CanBeAppliedToClassAndMethod()
    {
        AttributeUsageAttribute? usage = typeof(PermissionAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .FirstOrDefault();

        usage.ShouldNotBeNull();
        (usage!.ValidOn & AttributeTargets.Class).ShouldBe(AttributeTargets.Class);
        (usage.ValidOn & AttributeTargets.Method).ShouldBe(AttributeTargets.Method);
    }

    // =========================================================================
    // Policy — different permission names
    // =========================================================================

    [Theory]
    [InlineData("BlobStorage.Blobs.Read")]
    [InlineData("Administration.Users.Manage")]
    [InlineData("Workflow.Transitions.Execute")]
    public void Constructor_VariousPermissions_PolicyMatchesPermissionName(string permissionName)
    {
        PermissionAttribute attr = new(permissionName);

        attr.Policy.ShouldBe(permissionName);
    }
}
