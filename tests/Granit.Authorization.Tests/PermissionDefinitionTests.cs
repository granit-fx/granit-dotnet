using Granit.Localization;
using Shouldly;
using Xunit;

namespace Granit.Authorization.Tests;

public sealed class PermissionDefinitionTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var displayName = LocalizableString.Fixed("Delete invoices");
        PermissionDefinition definition = new("Invoices.Delete", displayName, "Invoices");

        definition.Name.ShouldBe("Invoices.Delete");
        definition.DisplayName.ShouldBe(displayName);
        definition.GroupName.ShouldBe("Invoices");
    }

    [Fact]
    public void Constructor_NullDisplayName_AllowsNull()
    {
        PermissionDefinition definition = new("Invoices.Read", null, "Invoices");

        definition.DisplayName.ShouldBeNull();
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        PermissionDefinition first = new("Invoices.Read", null, "Invoices");
        PermissionDefinition second = new("Invoices.Read", null, "Invoices");

        first.ShouldBe(second);
    }

    [Fact]
    public void Equality_DifferentName_AreNotEqual()
    {
        PermissionDefinition first = new("Invoices.Read", null, "Invoices");
        PermissionDefinition second = new("Invoices.Write", null, "Invoices");

        first.ShouldNotBe(second);
    }

    // =========================================================================
    // Equality — different groups
    // =========================================================================

    [Fact]
    public void Equality_DifferentGroupName_AreNotEqual()
    {
        PermissionDefinition first = new("Read", null, "Invoices");
        PermissionDefinition second = new("Read", null, "Orders");

        first.ShouldNotBe(second);
    }

    [Fact]
    public void Equality_DifferentDisplayName_AreNotEqual()
    {
        PermissionDefinition first = new("Invoices.Read", LocalizableString.Fixed("Read"), "Invoices");
        PermissionDefinition second = new("Invoices.Read", LocalizableString.Fixed("View"), "Invoices");

        first.ShouldNotBe(second);
    }

    // =========================================================================
    // Record semantics
    // =========================================================================

    [Fact]
    public void IsRecord_HasValueEquality()
    {
        var displayName = LocalizableString.Fixed("Delete");
        PermissionDefinition original = new("Invoices.Delete", displayName, "Invoices");
        PermissionDefinition copy = original with { };

        copy.ShouldBe(original);
        ReferenceEquals(original, copy).ShouldBeFalse();
    }

    [Fact]
    public void ToString_ContainsPermissionName()
    {
        PermissionDefinition definition = new("Invoices.Read", null, "Invoices");

        definition.ToString().ShouldContain("Invoices.Read");
    }
}
