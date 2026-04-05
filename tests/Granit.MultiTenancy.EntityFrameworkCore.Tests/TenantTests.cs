using Granit.Domain;
using Granit.Events;
using Granit.MultiTenancy;
using Granit.MultiTenancy.EntityFrameworkCore.Entities;
using Granit.MultiTenancy.Events;
using Shouldly;
using Xunit;

namespace Granit.MultiTenancy.EntityFrameworkCore.Tests;

public sealed class TenantTests
{
    // -------------------------------------------------------------------------
    // Create
    // -------------------------------------------------------------------------

    [Fact]
    public void Create_ValidParameters_ReturnsTenant()
    {
        var id = Guid.NewGuid();

        var tenant = Tenant.Create(id, "Acme Corp", "acme-corp", "admin@acme.com");

        tenant.Id.ShouldBe(id);
        tenant.Name.ShouldBe("Acme Corp");
        tenant.Identifier.ShouldBe("acme-corp");
        tenant.ContactEmail.ShouldBe("admin@acme.com");
        tenant.IsActive.ShouldBeTrue();
        tenant.Jurisdiction.ShouldBeNull();
    }

    [Fact]
    public void Create_JurisdictionDefaultsToNull()
    {
        var tenant = Tenant.Create(Guid.NewGuid(), "Acme Corp", "acme-corp", "admin@acme.com");

        tenant.Jurisdiction.ShouldBeNull();
    }

    [Fact]
    public void Create_WithoutContactEmail_SetsNull()
    {
        var tenant = Tenant.Create(Guid.NewGuid(), "Acme Corp", "acme-corp");

        tenant.ContactEmail.ShouldBeNull();
    }

    [Fact]
    public void Create_RaisesTenantCreatedEvent()
    {
        var id = Guid.NewGuid();

        var tenant = Tenant.Create(id, "Acme Corp", "acme-corp");

        IDomainEvent evt = tenant.DomainEvents.ShouldHaveSingleItem();
        TenantCreatedEvent created = evt.ShouldBeOfType<TenantCreatedEvent>();
        created.TenantId.ShouldBe(id);
        created.Name.ShouldBe("Acme Corp");
        created.Identifier.ShouldBe("acme-corp");
    }

    [Fact]
    public void Create_NullName_Throws()
    {
        Should.Throw<ArgumentException>(() => Tenant.Create(Guid.NewGuid(), null!, "acme"));
    }

    [Fact]
    public void Create_EmptyIdentifier_Throws()
    {
        Should.Throw<ArgumentException>(() => Tenant.Create(Guid.NewGuid(), "Acme", ""));
    }

    // -------------------------------------------------------------------------
    // UpdateDetails
    // -------------------------------------------------------------------------

    [Fact]
    public void UpdateDetails_ChangesNameAndEmail()
    {
        var tenant = Tenant.Create(Guid.NewGuid(), "Old Name", "acme");
        tenant.ClearDomainEvents();

        tenant.UpdateDetails("New Name", "new@acme.com", null);

        tenant.Name.ShouldBe("New Name");
        tenant.ContactEmail.ShouldBe("new@acme.com");
    }

    [Fact]
    public void UpdateDetails_RaisesTenantUpdatedEvent()
    {
        var tenant = Tenant.Create(Guid.NewGuid(), "Acme", "acme");
        tenant.ClearDomainEvents();

        tenant.UpdateDetails("New Name", "new@acme.com", null);

        IDomainEvent evt = tenant.DomainEvents.ShouldHaveSingleItem();
        TenantUpdatedEvent updated = evt.ShouldBeOfType<TenantUpdatedEvent>();
        updated.Name.ShouldBe("New Name");
        updated.ContactEmail.ShouldBe("new@acme.com");
    }

    [Fact]
    public void UpdateDetails_NullName_Throws()
    {
        var tenant = Tenant.Create(Guid.NewGuid(), "Acme", "acme");

        Should.Throw<ArgumentException>(() => tenant.UpdateDetails(null!, null, null));
    }

    // -------------------------------------------------------------------------
    // Activate / Deactivate
    // -------------------------------------------------------------------------

    [Fact]
    public void Deactivate_SetsIsActiveFalse()
    {
        var tenant = Tenant.Create(Guid.NewGuid(), "Acme", "acme");
        tenant.ClearDomainEvents();

        tenant.Deactivate();

        tenant.IsActive.ShouldBeFalse();
    }

    [Fact]
    public void Deactivate_RaisesTenantDeactivatedEvent()
    {
        var id = Guid.NewGuid();
        var tenant = Tenant.Create(id, "Acme", "acme");
        tenant.ClearDomainEvents();

        tenant.Deactivate();

        IDomainEvent evt = tenant.DomainEvents.ShouldHaveSingleItem();
        TenantDeactivatedEvent deactivated = evt.ShouldBeOfType<TenantDeactivatedEvent>();
        deactivated.TenantId.ShouldBe(id);
    }

    [Fact]
    public void Deactivate_AlreadyInactive_NoEvent()
    {
        var tenant = Tenant.Create(Guid.NewGuid(), "Acme", "acme");
        tenant.Deactivate();
        tenant.ClearDomainEvents();

        tenant.Deactivate();

        tenant.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void Activate_AfterDeactivate_SetsIsActiveTrue()
    {
        var tenant = Tenant.Create(Guid.NewGuid(), "Acme", "acme");
        tenant.Deactivate();
        tenant.ClearDomainEvents();

        tenant.Activate();

        tenant.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void Activate_RaisesTenantActivatedEvent()
    {
        var id = Guid.NewGuid();
        var tenant = Tenant.Create(id, "Acme", "acme");
        tenant.Deactivate();
        tenant.ClearDomainEvents();

        tenant.Activate();

        IDomainEvent evt = tenant.DomainEvents.ShouldHaveSingleItem();
        TenantActivatedEvent activated = evt.ShouldBeOfType<TenantActivatedEvent>();
        activated.TenantId.ShouldBe(id);
    }

    [Fact]
    public void Activate_AlreadyActive_NoEvent()
    {
        var tenant = Tenant.Create(Guid.NewGuid(), "Acme", "acme");
        tenant.ClearDomainEvents();

        tenant.Activate();

        tenant.DomainEvents.ShouldBeEmpty();
    }

    // -------------------------------------------------------------------------
    // Type conventions
    // -------------------------------------------------------------------------

    [Fact]
    public void Class_IsSealed() =>
        typeof(Tenant).IsSealed.ShouldBeTrue();

    [Fact]
    public void Inherits_FullAuditedAggregateRoot() =>
        typeof(Tenant).IsAssignableTo(typeof(FullAuditedAggregateRoot)).ShouldBeTrue();

    [Fact]
    public void Implements_ITenantInfo() =>
        typeof(Tenant).IsAssignableTo(typeof(ITenantInfo)).ShouldBeTrue();
}
