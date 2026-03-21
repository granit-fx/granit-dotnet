using Granit.Authorization.Abstractions;
using Granit.Core.Localization;
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
}
