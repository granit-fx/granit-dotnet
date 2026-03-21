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
}
