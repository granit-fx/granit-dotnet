using Granit.Contacts.Domain;
using Granit.Contacts.Domain.ValueObjects;
using Granit.Contacts.Events;
using Shouldly;
using Xunit;

namespace Granit.Contacts.Tests.Domain;

public sealed class ContactTests
{
    private static Contact NewIndividual(Guid? tenantId = null) =>
        Contact.Create(Guid.NewGuid(), tenantId, ContactKind.Individual, "Jean Dupont", "EUR");

    private static Contact NewCompany(Guid? tenantId = null) =>
        Contact.Create(Guid.NewGuid(), tenantId, ContactKind.Company, "Acme Corp", "EUR");

    // ── Factory ───────────────────────────────────────────────────

    [Fact]
    public void Create_WithMinimumArgs_StartsActive_AsCustomer()
    {
        var id = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        var c = Contact.Create(id, tenantId, ContactKind.Company, "Acme Corp", "eur");

        c.Id.ShouldBe(id);
        c.TenantId.ShouldBe(tenantId);
        c.Kind.ShouldBe(ContactKind.Company);
        c.Name.ShouldBe("Acme Corp");
        c.DefaultCurrency.ShouldBe("EUR");
        c.Status.ShouldBe(ContactStatus.Active);
        c.Roles.ShouldBe(ContactRoles.Customer);
        c.Timezone.ShouldBe("UTC");
        c.ExternalMappings.ShouldBeEmpty();
        c.UserId.ShouldBeNull();
        c.ParentContactId.ShouldBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Create_BlankName_Throws(string name) =>
        Should.Throw<ArgumentException>(() =>
            Contact.Create(Guid.NewGuid(), null, ContactKind.Individual, name, "EUR"));

    [Theory]
    [InlineData("EU")]
    [InlineData("EURO")]
    public void Create_InvalidCurrencyLength_Throws(string currency) =>
        Should.Throw<ArgumentException>(() =>
            Contact.Create(Guid.NewGuid(), null, ContactKind.Individual, "X", currency));

    [Fact]
    public void Create_WithAllOptionalFields_StoresThem()
    {
        var addr = Address.Create("rue de la Loi 16", "Brussels", "1000", "BE");

        var c = Contact.Create(
            Guid.NewGuid(), null, ContactKind.Company, "Acme", "EUR",
            roles: ContactRoles.Customer | ContactRoles.Supplier,
            email: "billing@acme.com",
            phone: "+3221234567",
            mobilePhone: "+32475123456",
            website: "https://acme.com",
            language: "fr-BE",
            timezone: "Europe/Brussels",
            taxId: "BE0123456789",
            registrationNumber: "0123.456.789",
            address: addr);

        c.Email.ShouldBe("billing@acme.com");
        c.Phone.ShouldBe("+3221234567");
        c.MobilePhone.ShouldBe("+32475123456");
        c.Website.ShouldBe("https://acme.com");
        c.Language.ShouldBe("fr-BE");
        c.Timezone.ShouldBe("Europe/Brussels");
        c.TaxId.ShouldBe("BE0123456789");
        c.RegistrationNumber.ShouldBe("0123.456.789");
        c.Address.ShouldBe(addr);
        c.HasRole(ContactRoles.Customer).ShouldBeTrue();
        c.HasRole(ContactRoles.Supplier).ShouldBeTrue();
        c.HasRole(ContactRoles.Employee).ShouldBeFalse();
    }

    [Fact]
    public void Create_RaisesContactCreatedEvents()
    {
        Contact c = NewCompany();

        c.DomainEvents.OfType<ContactCreatedEvent>().ShouldHaveSingleItem();
        c.IntegrationEvents.OfType<ContactCreatedEto>().ShouldHaveSingleItem();
    }

    // ── Lifecycle ─────────────────────────────────────────────────

    [Fact]
    public void Suspend_FromActive_TransitionsAndRaisesEvents()
    {
        Contact c = NewCompany();

        bool result = c.Suspend(reason: "unpaid balance");

        result.ShouldBeTrue();
        c.Status.ShouldBe(ContactStatus.Suspended);
        c.DomainEvents.OfType<ContactSuspendedEvent>().ShouldHaveSingleItem()
            .Reason.ShouldBe("unpaid balance");
        c.IntegrationEvents.OfType<ContactSuspendedEto>().ShouldHaveSingleItem();
    }

    [Fact]
    public void Suspend_AlreadySuspended_IsIdempotent_ReturnsFalse()
    {
        Contact c = NewCompany();
        c.Suspend();
        c.Suspend().ShouldBeFalse();
        c.DomainEvents.OfType<ContactSuspendedEvent>().Count().ShouldBe(1);
    }

    [Fact]
    public void Activate_FromSuspended_RestoresActive()
    {
        Contact c = NewCompany();
        c.Suspend();

        bool result = c.Activate();

        result.ShouldBeTrue();
        c.Status.ShouldBe(ContactStatus.Active);
        c.DomainEvents.OfType<ContactActivatedEvent>().ShouldHaveSingleItem();
    }

    [Fact]
    public void Activate_AlreadyActive_ReturnsFalse() =>
        NewCompany().Activate().ShouldBeFalse();

    [Fact]
    public void Archive_FromActive_TransitionsToTerminalState()
    {
        Contact c = NewCompany();

        c.Archive().ShouldBeTrue();
        c.Status.ShouldBe(ContactStatus.Archived);
        c.DomainEvents.OfType<ContactArchivedEvent>().ShouldHaveSingleItem();
    }

    [Fact]
    public void Archive_FromSuspended_AlsoTerminates()
    {
        Contact c = NewCompany();
        c.Suspend();

        c.Archive().ShouldBeTrue();
        c.Status.ShouldBe(ContactStatus.Archived);
    }

    [Fact]
    public void Activate_OnArchived_Throws()
    {
        Contact c = NewCompany();
        c.Archive();
        Should.Throw<InvalidOperationException>(() => c.Activate());
    }

    [Fact]
    public void Suspend_OnArchived_Throws()
    {
        Contact c = NewCompany();
        c.Archive();
        Should.Throw<InvalidOperationException>(() => c.Suspend());
    }

    // ── Identity & address updates ────────────────────────────────

    [Fact]
    public void UpdateContact_OnActive_AppliesAndRaisesEvent()
    {
        Contact c = NewCompany();

        c.UpdateContact("New Acme", email: "new@acme.com", website: "https://new.acme.com");

        c.Name.ShouldBe("New Acme");
        c.Email.ShouldBe("new@acme.com");
        c.Website.ShouldBe("https://new.acme.com");
        c.DomainEvents.OfType<ContactUpdatedEvent>().ShouldHaveSingleItem();
    }

    [Fact]
    public void UpdateContact_OnArchived_Throws()
    {
        Contact c = NewCompany();
        c.Archive();
        Should.Throw<InvalidOperationException>(() => c.UpdateContact("X"));
    }

    [Fact]
    public void UpdateAddress_AssignsAndRaises()
    {
        Contact c = NewCompany();
        var addr = Address.Create("L1", "C", "1000", "BE");

        c.UpdateAddress(addr);

        c.Address.ShouldBe(addr);
        c.DomainEvents.OfType<ContactUpdatedEvent>().ShouldHaveSingleItem();
    }

    [Fact]
    public void UpdateAddress_Null_ClearsExisting()
    {
        Contact c = NewCompany();
        c.UpdateAddress(Address.Create("L1", "C", "1000", "BE"));

        c.UpdateAddress(null);

        c.Address.ShouldBeNull();
    }

    [Fact]
    public void UpdateTaxIdentity_StoresValues()
    {
        Contact c = NewCompany();

        c.UpdateTaxIdentity("BE0123456789", "0123.456.789");

        c.TaxId.ShouldBe("BE0123456789");
        c.RegistrationNumber.ShouldBe("0123.456.789");
        c.DomainEvents.OfType<ContactUpdatedEvent>().ShouldHaveSingleItem();
    }

    // ── Hierarchy ─────────────────────────────────────────────────

    [Fact]
    public void AttachToParent_SameTenant_Succeeds()
    {
        var tenantId = Guid.NewGuid();
        Contact child = NewIndividual(tenantId);
        var parentId = ContactId.Create(Guid.NewGuid());

        child.AttachToParent(parentId, parentTenantId: tenantId);

        child.ParentContactId.ShouldBe(parentId);
        child.DomainEvents.OfType<ContactAttachedToParentEvent>().ShouldHaveSingleItem();
    }

    [Fact]
    public void AttachToParent_HostScope_Succeeds()
    {
        Contact child = NewIndividual(tenantId: null);
        var parentId = ContactId.Create(Guid.NewGuid());

        child.AttachToParent(parentId, parentTenantId: null);

        child.ParentContactId.ShouldBe(parentId);
    }

    [Fact]
    public void AttachToParent_DifferentTenant_Throws()
    {
        Contact child = NewIndividual(tenantId: Guid.NewGuid());
        var parentId = ContactId.Create(Guid.NewGuid());

        Should.Throw<InvalidOperationException>(() =>
            child.AttachToParent(parentId, parentTenantId: Guid.NewGuid()));
    }

    [Fact]
    public void AttachToParent_Self_Throws()
    {
        Contact c = NewIndividual();
        var selfId = ContactId.Create(c.Id);

        Should.Throw<InvalidOperationException>(() =>
            c.AttachToParent(selfId, parentTenantId: c.TenantId));
    }

    [Fact]
    public void DetachFromParent_WhenAttached_ReturnsTrue()
    {
        Contact c = NewIndividual();
        c.AttachToParent(ContactId.Create(Guid.NewGuid()), c.TenantId);

        bool result = c.DetachFromParent();

        result.ShouldBeTrue();
        c.ParentContactId.ShouldBeNull();
        c.DomainEvents.OfType<ContactDetachedFromParentEvent>().ShouldHaveSingleItem();
    }

    [Fact]
    public void DetachFromParent_WhenNotAttached_ReturnsFalse() =>
        NewIndividual().DetachFromParent().ShouldBeFalse();

    // ── User linkage ──────────────────────────────────────────────

    [Fact]
    public void LinkToUser_OnIndividual_StoresUserId()
    {
        Contact c = NewIndividual();
        var userId = Guid.NewGuid();

        c.LinkToUser(userId);

        c.UserId.ShouldBe(userId);
        c.DomainEvents.OfType<ContactLinkedToUserEvent>().ShouldHaveSingleItem();
    }

    [Fact]
    public void LinkToUser_OnCompany_Throws()
    {
        Contact c = NewCompany();

        Should.Throw<InvalidOperationException>(() => c.LinkToUser(Guid.NewGuid()));
    }

    [Fact]
    public void LinkToUser_EmptyGuid_Throws()
    {
        Contact c = NewIndividual();

        Should.Throw<ArgumentException>(() => c.LinkToUser(Guid.Empty));
    }

    [Fact]
    public void UnlinkFromUser_WhenLinked_ClearsUserId()
    {
        Contact c = NewIndividual();
        c.LinkToUser(Guid.NewGuid());

        c.UnlinkFromUser().ShouldBeTrue();
        c.UserId.ShouldBeNull();
    }

    [Fact]
    public void UnlinkFromUser_WhenNotLinked_ReturnsFalse() =>
        NewIndividual().UnlinkFromUser().ShouldBeFalse();

    // ── Roles ─────────────────────────────────────────────────────

    [Fact]
    public void AddRole_NewRole_TogglesFlagAndRaisesEvent()
    {
        Contact c = NewCompany();
        c.HasRole(ContactRoles.Supplier).ShouldBeFalse();

        c.AddRole(ContactRoles.Supplier).ShouldBeTrue();

        c.HasRole(ContactRoles.Supplier).ShouldBeTrue();
        c.HasRole(ContactRoles.Customer).ShouldBeTrue(); // not removed
        c.DomainEvents.OfType<ContactRoleAddedEvent>().ShouldHaveSingleItem();
    }

    [Fact]
    public void AddRole_AlreadyPresent_IsIdempotent()
    {
        Contact c = NewCompany(); // has Customer

        c.AddRole(ContactRoles.Customer).ShouldBeFalse();
        c.DomainEvents.OfType<ContactRoleAddedEvent>().ShouldBeEmpty();
    }

    [Fact]
    public void AddRole_None_IsNoOp() =>
        NewCompany().AddRole(ContactRoles.None).ShouldBeFalse();

    [Fact]
    public void RemoveRole_KnownRole_ClearsAndRaisesEvent()
    {
        Contact c = NewCompany();

        c.RemoveRole(ContactRoles.Customer).ShouldBeTrue();
        c.HasRole(ContactRoles.Customer).ShouldBeFalse();
        c.Roles.ShouldBe(ContactRoles.None);
        c.DomainEvents.OfType<ContactRoleRemovedEvent>().ShouldHaveSingleItem();
    }

    [Fact]
    public void RemoveRole_NotPresent_IsIdempotent() =>
        NewCompany().RemoveRole(ContactRoles.Supplier).ShouldBeFalse();

    [Fact]
    public void Roles_CanCarryMultipleSimultaneously()
    {
        Contact c = NewCompany();

        c.AddRole(ContactRoles.Supplier);
        c.AddRole(ContactRoles.Lead);

        c.HasRole(ContactRoles.Customer).ShouldBeTrue();
        c.HasRole(ContactRoles.Supplier).ShouldBeTrue();
        c.HasRole(ContactRoles.Lead).ShouldBeTrue();
        c.HasRole(ContactRoles.Customer | ContactRoles.Supplier).ShouldBeTrue();
    }

    // ── External mappings ─────────────────────────────────────────

    [Fact]
    public void AddExternalMapping_NewProvider_AppendsAndRaisesEvents()
    {
        Contact c = NewCompany();

        c.AddExternalMapping(Guid.NewGuid(), ContactExternalProviderNames.Stripe, "cus_123");

        c.ExternalMappings.Count.ShouldBe(1);
        c.FindExternalId("stripe").ShouldBe("cus_123");
        c.DomainEvents.OfType<ContactExternalMappingAddedEvent>().ShouldHaveSingleItem();
        c.IntegrationEvents.OfType<ContactExternalMappingAddedEto>().ShouldHaveSingleItem();
    }

    [Fact]
    public void AddExternalMapping_DuplicateProvider_Throws()
    {
        Contact c = NewCompany();
        c.AddExternalMapping(Guid.NewGuid(), "stripe", "cus_1");

        Should.Throw<InvalidOperationException>(() =>
            c.AddExternalMapping(Guid.NewGuid(), "STRIPE", "cus_2"));
    }

    [Fact]
    public void RemoveExternalMapping_KnownProvider_ReturnsTrue()
    {
        Contact c = NewCompany();
        c.AddExternalMapping(Guid.NewGuid(), "stripe", "cus_1");

        c.RemoveExternalMapping("STRIPE").ShouldBeTrue(); // case-insensitive
        c.ExternalMappings.ShouldBeEmpty();
    }

    [Fact]
    public void RemoveExternalMapping_UnknownProvider_ReturnsFalse() =>
        NewCompany().RemoveExternalMapping("stripe").ShouldBeFalse();

    [Fact]
    public void FindExternalId_KnownProvider_ReturnsValue_CaseInsensitive()
    {
        Contact c = NewCompany();
        c.AddExternalMapping(Guid.NewGuid(), "odoo", "42");

        c.FindExternalId("odoo").ShouldBe("42");
        c.FindExternalId("ODOO").ShouldBe("42");
    }

    [Fact]
    public void FindExternalId_UnknownProvider_ReturnsNull() =>
        NewCompany().FindExternalId("stripe").ShouldBeNull();

    // ── Multi-tenancy ─────────────────────────────────────────────

    [Fact]
    public void Contact_HostScoped_HasNullTenantId() =>
        Contact.Create(Guid.NewGuid(), null, ContactKind.Company, "X", "EUR")
            .TenantId.ShouldBeNull();

    [Fact]
    public void Contact_ImplementsIMultiTenant() =>
        typeof(Contact).IsAssignableTo(typeof(Granit.Domain.IMultiTenant)).ShouldBeTrue();

    [Fact]
    public void Contact_ExplicitInterface_AllowsTenantIdMutation_ForInterceptor()
    {
        var c = Contact.Create(Guid.NewGuid(), null, ContactKind.Company, "X", "EUR");
        var newTenant = Guid.NewGuid();

        ((Granit.Domain.IMultiTenant)c).TenantId = newTenant;

        c.TenantId.ShouldBe(newTenant);
    }
}
