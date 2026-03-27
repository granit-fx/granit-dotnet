using Granit.Authorization.Events;
using Shouldly;
using Xunit;

namespace Granit.Authorization.Tests;

public sealed class PermissionGrantChangedEventTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var tenantId = Guid.NewGuid();

        PermissionGrantChangedEvent @event = new("Invoices.Delete", "accountant", tenantId, true);

        @event.PermissionName.ShouldBe("Invoices.Delete");
        @event.RoleName.ShouldBe("accountant");
        @event.TenantId.ShouldBe(tenantId);
        @event.IsGranted.ShouldBeTrue();
    }

    [Fact]
    public void Constructor_WithNullTenantId_SetsNull()
    {
        PermissionGrantChangedEvent @event = new("Invoices.Read", "editor", null, false);

        @event.TenantId.ShouldBeNull();
        @event.IsGranted.ShouldBeFalse();
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var tenantId = Guid.NewGuid();
        PermissionGrantChangedEvent first = new("Invoices.Read", "editor", tenantId, true);
        PermissionGrantChangedEvent second = new("Invoices.Read", "editor", tenantId, true);

        first.ShouldBe(second);
    }

    [Fact]
    public void Equality_DifferentPermission_AreNotEqual()
    {
        var tenantId = Guid.NewGuid();
        PermissionGrantChangedEvent first = new("Invoices.Read", "editor", tenantId, true);
        PermissionGrantChangedEvent second = new("Invoices.Write", "editor", tenantId, true);

        first.ShouldNotBe(second);
    }

    // =========================================================================
    // Equality — different fields
    // =========================================================================

    [Fact]
    public void Equality_DifferentRoleName_AreNotEqual()
    {
        var tenantId = Guid.NewGuid();
        PermissionGrantChangedEvent first = new("Invoices.Read", "editor", tenantId, true);
        PermissionGrantChangedEvent second = new("Invoices.Read", "viewer", tenantId, true);

        first.ShouldNotBe(second);
    }

    [Fact]
    public void Equality_DifferentTenantId_AreNotEqual()
    {
        PermissionGrantChangedEvent first = new("Invoices.Read", "editor", Guid.NewGuid(), true);
        PermissionGrantChangedEvent second = new("Invoices.Read", "editor", Guid.NewGuid(), true);

        first.ShouldNotBe(second);
    }

    [Fact]
    public void Equality_DifferentIsGranted_AreNotEqual()
    {
        var tenantId = Guid.NewGuid();
        PermissionGrantChangedEvent first = new("Invoices.Read", "editor", tenantId, true);
        PermissionGrantChangedEvent second = new("Invoices.Read", "editor", tenantId, false);

        first.ShouldNotBe(second);
    }

    // =========================================================================
    // Record semantics — with expression
    // =========================================================================

    [Fact]
    public void WithExpression_CreatesModifiedCopy()
    {
        var tenantId = Guid.NewGuid();
        PermissionGrantChangedEvent original = new("Invoices.Read", "editor", tenantId, true);

        PermissionGrantChangedEvent modified = original with { IsGranted = false };

        modified.IsGranted.ShouldBeFalse();
        modified.PermissionName.ShouldBe("Invoices.Read");
        modified.RoleName.ShouldBe("editor");
        modified.TenantId.ShouldBe(tenantId);
    }
}
