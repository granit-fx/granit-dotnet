using Granit.Authorization.Domain;
using Granit.Authorization.Events;
using Granit.Domain;
using Granit.MultiTenancy;
using Shouldly;
using Xunit;

namespace Granit.Authorization.EntityFrameworkCore.Tests;

public sealed class RoleMetadataEntityTests
{
    [Fact]
    public void InheritsFromAuditedAggregateRoot()
    {
        RoleMetadata role = NewHostRole();

        role.ShouldBeAssignableTo<AuditedAggregateRoot>();
    }

    [Fact]
    public void ImplementsIMultiTenant()
    {
        RoleMetadata role = NewHostRole();

        role.ShouldBeAssignableTo<IMultiTenant>();
    }

    [Fact]
    public void Create_Host_PopulatesAllProperties()
    {
        var id = Guid.NewGuid();

        var role = RoleMetadata.Create(
            id, "SuperAdmin", MultiTenancySides.Host, tenantId: null,
            clientId: null, description: "Platform administrator", isSystem: true);

        role.Id.ShouldBe(id);
        role.Name.ShouldBe("SuperAdmin");
        role.MultiTenancySides.ShouldBe(MultiTenancySides.Host);
        role.TenantId.ShouldBeNull();
        role.ClientId.ShouldBeNull();
        role.Description.ShouldBe("Platform administrator");
        role.IsSystem.ShouldBeTrue();
    }

    [Fact]
    public void Create_Tenant_RequiresTenantId()
    {
        var tenantId = Guid.NewGuid();

        var role = RoleMetadata.Create(
            Guid.NewGuid(), "Manager", MultiTenancySides.Tenant, tenantId);

        role.TenantId.ShouldBe(tenantId);
        role.MultiTenancySides.ShouldBe(MultiTenancySides.Tenant);
    }

    [Fact]
    public void Create_Both_LeavesTenantIdNull()
    {
        var role = RoleMetadata.Create(
            Guid.NewGuid(), "User", MultiTenancySides.Both, tenantId: null);

        role.TenantId.ShouldBeNull();
        role.MultiTenancySides.ShouldBe(MultiTenancySides.Both);
    }

    [Fact]
    public void Create_RaisesRoleCreatedEvent()
    {
        var id = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        var role = RoleMetadata.Create(
            id, "Manager", MultiTenancySides.Tenant, tenantId, clientId: "client-a");

        role.DomainEvents.Count.ShouldBe(1);
        RoleCreatedEvent evt = role.DomainEvents.Single().ShouldBeOfType<RoleCreatedEvent>();
        evt.RoleId.ShouldBe(id);
        evt.Name.ShouldBe("Manager");
        evt.MultiTenancySides.ShouldBe(MultiTenancySides.Tenant);
        evt.TenantId.ShouldBe(tenantId);
        evt.ClientId.ShouldBe("client-a");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Create_RejectsNullOrWhitespaceName(string? name)
    {
        Should.Throw<ArgumentException>(() =>
            RoleMetadata.Create(Guid.NewGuid(), name!, MultiTenancySides.Host, null));
    }

    [Fact]
    public void Create_RejectsNameOver256Chars()
    {
        string longName = new('x', 257);

        Should.Throw<ArgumentException>(() =>
            RoleMetadata.Create(Guid.NewGuid(), longName, MultiTenancySides.Host, null));
    }

    [Fact]
    public void Create_RejectsClientIdOver256Chars()
    {
        string longClient = new('c', 257);

        Should.Throw<ArgumentException>(() =>
            RoleMetadata.Create(Guid.NewGuid(), "Manager", MultiTenancySides.Host, null, clientId: longClient));
    }

    [Fact]
    public void Create_RejectsDescriptionOver2048Chars()
    {
        string longDesc = new('d', 2049);

        Should.Throw<ArgumentException>(() =>
            RoleMetadata.Create(Guid.NewGuid(), "Manager", MultiTenancySides.Host, null, description: longDesc));
    }

    [Fact]
    public void Create_Host_WithTenantId_Throws()
    {
        Should.Throw<ArgumentException>(() =>
            RoleMetadata.Create(Guid.NewGuid(), "SuperAdmin", MultiTenancySides.Host, Guid.NewGuid()));
    }

    [Fact]
    public void Create_Both_WithTenantId_Throws()
    {
        Should.Throw<ArgumentException>(() =>
            RoleMetadata.Create(Guid.NewGuid(), "User", MultiTenancySides.Both, Guid.NewGuid()));
    }

    [Fact]
    public void Create_Tenant_WithoutTenantId_Throws()
    {
        Should.Throw<ArgumentException>(() =>
            RoleMetadata.Create(Guid.NewGuid(), "Manager", MultiTenancySides.Tenant, tenantId: null));
    }

    [Fact]
    public void Rename_ChangesNameAndDescription_RaisesEventWithPreviousName()
    {
        RoleMetadata role = NewHostRole();
        string originalName = role.Name;
        role.ClearDomainEvents();

        role.Rename("PlatformAdmin", "Updated description");

        role.Name.ShouldBe("PlatformAdmin");
        role.Description.ShouldBe("Updated description");
        role.DomainEvents.Count.ShouldBe(1);
        RoleUpdatedEvent evt = role.DomainEvents.Single().ShouldBeOfType<RoleUpdatedEvent>();
        evt.Name.ShouldBe("PlatformAdmin");
        evt.PreviousName.ShouldBe(originalName);
    }

    [Fact]
    public void Rename_DescriptionOnly_RaisesEventWithNullPreviousName()
    {
        RoleMetadata role = NewHostRole();
        role.ClearDomainEvents();

        role.Rename(role.Name, "New description only");

        role.DomainEvents.Count.ShouldBe(1);
        RoleUpdatedEvent evt = role.DomainEvents.Single().ShouldBeOfType<RoleUpdatedEvent>();
        evt.PreviousName.ShouldBeNull();
    }

    [Fact]
    public void Rename_NoChanges_IsNoOp()
    {
        RoleMetadata role = NewHostRole();
        role.ClearDomainEvents();
        string originalName = role.Name;
        string? originalDesc = role.Description;

        role.Rename(originalName, originalDesc);

        role.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void MarkAsDeleted_RaisesRoleDeletedEvent()
    {
        RoleMetadata role = NewHostRole();
        role.ClearDomainEvents();

        role.MarkAsDeleted();

        role.DomainEvents.Count.ShouldBe(1);
        RoleDeletedEvent evt = role.DomainEvents.Single().ShouldBeOfType<RoleDeletedEvent>();
        evt.RoleId.ShouldBe(role.Id);
        evt.Name.ShouldBe(role.Name);
    }

    private static RoleMetadata NewHostRole() =>
        RoleMetadata.Create(
            Guid.NewGuid(),
            "SuperAdmin",
            MultiTenancySides.Host,
            tenantId: null,
            description: "Initial description");
}
