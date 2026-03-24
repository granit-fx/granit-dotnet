using Granit.Authorization.Abstractions;
using Granit.Localization;
using Shouldly;
using Xunit;

namespace Granit.Authorization.Tests;

public sealed class PermissionGroupTests
{
    [Fact]
    public void Constructor_SetsName()
    {
        PermissionGroup group = new("Invoices");

        group.Name.ShouldBe("Invoices");
    }

    [Fact]
    public void Constructor_SetsDisplayName()
    {
        var displayName = LocalizableString.Fixed("Invoice Management");
        PermissionGroup group = new("Invoices", displayName);

        group.DisplayName.ShouldBe(displayName);
    }

    [Fact]
    public void Constructor_NullDisplayName_DefaultsToNull()
    {
        PermissionGroup group = new("Invoices");

        group.DisplayName.ShouldBeNull();
    }

    [Fact]
    public void Permissions_Initially_IsEmpty()
    {
        PermissionGroup group = new("Invoices");

        group.Permissions.ShouldBeEmpty();
    }

    [Fact]
    public void AddPermission_SinglePermission_AppearsInList()
    {
        PermissionGroup group = new("Invoices");

        PermissionDefinition definition = group.AddPermission("Invoices.Read");

        group.Permissions.ShouldHaveSingleItem();
        group.Permissions[0].ShouldBe(definition);
    }

    [Fact]
    public void AddPermission_ReturnsDefinitionWithCorrectGroupName()
    {
        PermissionGroup group = new("Invoices");

        PermissionDefinition definition = group.AddPermission("Invoices.Read");

        definition.GroupName.ShouldBe("Invoices");
        definition.Name.ShouldBe("Invoices.Read");
    }

    [Fact]
    public void AddPermission_WithDisplayName_SetsDisplayName()
    {
        PermissionGroup group = new("Invoices");
        var displayName = LocalizableString.Fixed("Read invoices");

        PermissionDefinition definition = group.AddPermission("Invoices.Read", displayName);

        definition.DisplayName.ShouldBe(displayName);
    }

    [Fact]
    public void AddPermission_MultiplePermissions_AllPresent()
    {
        PermissionGroup group = new("Invoices");

        group.AddPermission("Invoices.Read");
        group.AddPermission("Invoices.Create");
        group.AddPermission("Invoices.Delete");

        group.Permissions.Count.ShouldBe(3);
        group.Permissions.Select(p => p.Name).ShouldBe(
            ["Invoices.Read", "Invoices.Create", "Invoices.Delete"]);
    }

    [Fact]
    public void Permissions_ReturnsReadOnlyList()
    {
        PermissionGroup group = new("Invoices");
        group.AddPermission("Invoices.Read");

        group.Permissions.ShouldBeAssignableTo<IReadOnlyList<PermissionDefinition>>();
    }
}
