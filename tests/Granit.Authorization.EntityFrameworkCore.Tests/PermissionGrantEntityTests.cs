using Granit.Authorization;
using Granit.Authorization.Domain;
using Granit.Authorization.Events;
using Granit.Domain;
using Shouldly;
using Xunit;

namespace Granit.Authorization.EntityFrameworkCore.Tests;

public sealed class PermissionGrantEntityTests
{
    [Fact]
    public void InheritsFromAuditedAggregateRoot()
    {
        PermissionGrant grant = NewGrant();

        grant.ShouldBeAssignableTo<AuditedAggregateRoot>();
    }

    [Fact]
    public void ImplementsIMultiTenant()
    {
        PermissionGrant grant = NewGrant();

        grant.ShouldBeAssignableTo<IMultiTenant>();
    }

    [Fact]
    public void Create_PopulatesAllProperties()
    {
        var id = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        var grant = PermissionGrant.Create(
            id,
            "Invoices.Delete",
            PermissionGrantProviderNames.Role,
            "accountant",
            tenantId);

        grant.Id.ShouldBe(id);
        grant.Name.ShouldBe("Invoices.Delete");
        grant.ProviderName.ShouldBe("R");
        grant.ProviderKey.ShouldBe("accountant");
        grant.TenantId.ShouldBe(tenantId);
    }

    [Fact]
    public void Create_HostScoped_LeavesTenantIdNull()
    {
        var grant = PermissionGrant.Create(
            Guid.NewGuid(), "Invoices.Delete", "R", "accountant", tenantId: null);

        grant.TenantId.ShouldBeNull();
    }

    [Fact]
    public void Create_RaisesGrantChangedEvent_IsGrantedTrue()
    {
        var tenantId = Guid.NewGuid();

        var grant = PermissionGrant.Create(
            Guid.NewGuid(), "Invoices.Delete", "R", "accountant", tenantId);

        grant.DomainEvents.Count.ShouldBe(1);
        PermissionGrantChangedEvent evt = grant.DomainEvents.Single().ShouldBeOfType<PermissionGrantChangedEvent>();
        evt.PermissionName.ShouldBe("Invoices.Delete");
        evt.ProviderName.ShouldBe("R");
        evt.ProviderKey.ShouldBe("accountant");
        evt.TenantId.ShouldBe(tenantId);
        evt.IsGranted.ShouldBeTrue();
    }

    [Fact]
    public void MarkAsRevoked_RaisesGrantChangedEvent_IsGrantedFalse()
    {
        var grant = PermissionGrant.Create(
            Guid.NewGuid(), "Invoices.Delete", "R", "accountant", tenantId: null);
        grant.ClearDomainEvents();

        grant.MarkAsRevoked();

        grant.DomainEvents.Count.ShouldBe(1);
        PermissionGrantChangedEvent evt = grant.DomainEvents.Single().ShouldBeOfType<PermissionGrantChangedEvent>();
        evt.PermissionName.ShouldBe("Invoices.Delete");
        evt.IsGranted.ShouldBeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Create_RejectsNullOrWhitespacePermissionName(string? permissionName)
    {
        Should.Throw<ArgumentException>(() =>
            PermissionGrant.Create(Guid.NewGuid(), permissionName!, "R", "accountant", null));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Create_RejectsNullOrWhitespaceProviderName(string? providerName)
    {
        Should.Throw<ArgumentException>(() =>
            PermissionGrant.Create(Guid.NewGuid(), "Invoices.Delete", providerName!, "accountant", null));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Create_RejectsNullOrWhitespaceProviderKey(string? providerKey)
    {
        Should.Throw<ArgumentException>(() =>
            PermissionGrant.Create(Guid.NewGuid(), "Invoices.Delete", "R", providerKey!, null));
    }

    [Fact]
    public void Create_RejectsPermissionNameOver256Chars()
    {
        string longName = new('x', 257);

        Should.Throw<ArgumentException>(() =>
            PermissionGrant.Create(Guid.NewGuid(), longName, "R", "accountant", null));
    }

    [Fact]
    public void Create_RejectsProviderNameOver8Chars()
    {
        Should.Throw<ArgumentException>(() =>
            PermissionGrant.Create(Guid.NewGuid(), "Invoices.Delete", "ProviderTooLong", "accountant", null));
    }

    [Fact]
    public void Create_RejectsProviderKeyOver256Chars()
    {
        string longKey = new('x', 257);

        Should.Throw<ArgumentException>(() =>
            PermissionGrant.Create(Guid.NewGuid(), "Invoices.Delete", "R", longKey, null));
    }

    private static PermissionGrant NewGrant() =>
        PermissionGrant.Create(Guid.NewGuid(), "Sample.Perm", "R", "role-a", null);
}
