using Granit.Authorization.Services;
using Granit.Localization;
using Shouldly;
using Xunit;

namespace Granit.Authorization.Tests;

public sealed class PermissionDefinitionContextTests
{
    [Fact]
    public void AddGroup_NewGroup_ReturnsNewGroupAndAddsToGroups()
    {
        PermissionDefinitionContext context = new();

        PermissionGroup group = context.AddGroup("Invoices");

        group.Name.ShouldBe("Invoices");
        context.Groups.ShouldContainKey("Invoices");
    }

    [Fact]
    public void AddGroup_WithDisplayName_SetsDisplayName()
    {
        PermissionDefinitionContext context = new();
        var displayName = LocalizableString.Fixed("Invoice Management");

        PermissionGroup group = context.AddGroup("Invoices", displayName);

        group.DisplayName.ShouldBe(displayName);
    }

    [Fact]
    public void AddGroup_SameNameTwice_ReturnsSameInstance()
    {
        PermissionDefinitionContext context = new();

        PermissionGroup first = context.AddGroup("Invoices", LocalizableString.Fixed("First"));
        PermissionGroup second = context.AddGroup("Invoices", LocalizableString.Fixed("Second"));

        second.ShouldBeSameAs(first);
        context.Groups.Count.ShouldBe(1);
    }

    [Fact]
    public void AddGroup_SameNameTwice_PreservesOriginalDisplayName()
    {
        PermissionDefinitionContext context = new();
        var originalName = LocalizableString.Fixed("Original");

        context.AddGroup("Invoices", originalName);
        PermissionGroup group = context.AddGroup("Invoices", LocalizableString.Fixed("Overwritten"));

        group.DisplayName.ShouldBe(originalName);
    }

    [Fact]
    public void AddGroup_MultipleGroups_AllPresent()
    {
        PermissionDefinitionContext context = new();

        context.AddGroup("Invoices");
        context.AddGroup("Products");
        context.AddGroup("Users");

        context.Groups.Count.ShouldBe(3);
        context.Groups.Keys.ShouldBe(["Invoices", "Products", "Users"], ignoreOrder: true);
    }

    [Fact]
    public void Groups_IsReadOnly()
    {
        PermissionDefinitionContext context = new();

        context.Groups.ShouldBeAssignableTo<IReadOnlyDictionary<string, PermissionGroup>>();
    }

    [Fact]
    public void ImplementsIPermissionDefinitionContext()
    {
        PermissionDefinitionContext context = new();

        context.ShouldBeAssignableTo<IPermissionDefinitionContext>();
    }

    // =========================================================================
    // AddGroup — permission chaining
    // =========================================================================

    [Fact]
    public void AddGroup_ThenAddPermissions_PermissionsVisibleViaGroup()
    {
        PermissionDefinitionContext context = new();

        PermissionGroup group = context.AddGroup("Orders");
        group.AddPermission("Orders.Read");
        group.AddPermission("Orders.Create");

        context.Groups["Orders"].Permissions.Count.ShouldBe(2);
    }

    [Fact]
    public void AddGroup_SameGroupFromTwoCallers_PermissionsAccumulateOnSameInstance()
    {
        PermissionDefinitionContext context = new();

        PermissionGroup first = context.AddGroup("Shared");
        first.AddPermission("Shared.Alpha");

        PermissionGroup second = context.AddGroup("Shared");
        second.AddPermission("Shared.Beta");

        // Same instance, so both permissions are visible
        first.Permissions.Count.ShouldBe(2);
        second.Permissions.Count.ShouldBe(2);
        first.ShouldBeSameAs(second);
    }

    // =========================================================================
    // Groups — empty context
    // =========================================================================

    [Fact]
    public void Groups_EmptyContext_ReturnsEmptyDictionary()
    {
        PermissionDefinitionContext context = new();

        context.Groups.ShouldBeEmpty();
    }

    // =========================================================================
    // AddGroup — ordinal key comparison
    // =========================================================================

    [Fact]
    public void AddGroup_DifferentCase_CreatesSeparateGroups()
    {
        PermissionDefinitionContext context = new();

        PermissionGroup lower = context.AddGroup("invoices");
        PermissionGroup upper = context.AddGroup("Invoices");

        lower.ShouldNotBeSameAs(upper);
        context.Groups.Count.ShouldBe(2);
    }

    [Fact]
    public void AddGroup_WithoutDisplayName_GroupHasNullDisplayName()
    {
        PermissionDefinitionContext context = new();

        PermissionGroup group = context.AddGroup("NoDisplay");

        group.DisplayName.ShouldBeNull();
    }
}
