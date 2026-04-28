using Granit.Domain;
using Granit.Parties.Domain;
using Granit.Parties.Domain.ValueObjects;
using Granit.Parties.Events;
using Shouldly;
using Xunit;

namespace Granit.Parties.Tests.Domain;

public sealed class PartyTests
{
    private static Party NewIndividual(Guid? tenantId = null) =>
        Party.Create(Guid.NewGuid(), tenantId, PartyKind.Individual, "Jean Dupont", "EUR");

    private static Party NewCompany(Guid? tenantId = null) =>
        Party.Create(Guid.NewGuid(), tenantId, PartyKind.Company, "Acme Corp", "EUR");

    // ── Factory ───────────────────────────────────────────────────

    [Fact]
    public void Create_WithMinimumArgs_StartsActive_AsCustomer()
    {
        var id = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        var c = Party.Create(id, tenantId, PartyKind.Company, "Acme Corp", "eur");

        c.Id.ShouldBe(id);
        c.TenantId.ShouldBe(tenantId);
        c.Kind.ShouldBe(PartyKind.Company);
        c.Name.ShouldBe("Acme Corp");
        c.DefaultCurrency.ShouldBe("EUR");
        c.Status.ShouldBe(PartyStatus.Active);
        c.Roles.ShouldBe(PartyRoles.Customer);
        c.Timezone.ShouldBe("UTC");
        c.ExternalMappings.ShouldBeEmpty();
        c.UserId.ShouldBeNull();
        c.ParentPartyId.ShouldBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Create_BlankName_Throws(string name) =>
        Should.Throw<ArgumentException>(() =>
            Party.Create(Guid.NewGuid(), null, PartyKind.Individual, name, "EUR"));

    [Theory]
    [InlineData("EU")]
    [InlineData("EURO")]
    public void Create_InvalidCurrencyLength_Throws(string currency) =>
        Should.Throw<ArgumentException>(() =>
            Party.Create(Guid.NewGuid(), null, PartyKind.Individual, "X", currency));

    [Fact]
    public void Create_WithAllOptionalFields_StoresThem()
    {
        var c = Party.Create(
            Guid.NewGuid(), null, PartyKind.Company, "Acme", "EUR",
            roles: PartyRoles.Customer | PartyRoles.Supplier,
            website: "https://acme.com",
            language: "fr-BE",
            timezone: "Europe/Brussels",
            taxId: "BE0123456789",
            registrationNumber: "0123.456.789");

        c.Website.ShouldBe("https://acme.com");
        c.Language.ShouldBe("fr-BE");
        c.Timezone.ShouldBe("Europe/Brussels");
        c.TaxId.ShouldBe("BE0123456789");
        c.RegistrationNumber.ShouldBe("0123.456.789");
        c.Addresses.ShouldBeEmpty();
        c.Emails.ShouldBeEmpty();
        c.Phones.ShouldBeEmpty();
        c.HasRole(PartyRoles.Customer).ShouldBeTrue();
        c.HasRole(PartyRoles.Supplier).ShouldBeTrue();
        c.HasRole(PartyRoles.Employee).ShouldBeFalse();
    }

    [Fact]
    public void Create_RaisesPartyCreatedEvents()
    {
        Party c = NewCompany();

        c.DomainEvents.OfType<PartyCreatedEvent>().ShouldHaveSingleItem();
        c.IntegrationEvents.OfType<PartyCreatedEto>().ShouldHaveSingleItem();
    }

    // ── Lifecycle ─────────────────────────────────────────────────

    [Fact]
    public void Suspend_FromActive_TransitionsAndRaisesEvents()
    {
        Party c = NewCompany();

        bool result = c.Suspend(reason: "unpaid balance");

        result.ShouldBeTrue();
        c.Status.ShouldBe(PartyStatus.Suspended);
        c.DomainEvents.OfType<PartySuspendedEvent>().ShouldHaveSingleItem()
            .Reason.ShouldBe("unpaid balance");
        c.IntegrationEvents.OfType<PartySuspendedEto>().ShouldHaveSingleItem();
    }

    [Fact]
    public void Suspend_AlreadySuspended_IsIdempotent_ReturnsFalse()
    {
        Party c = NewCompany();
        c.Suspend();
        c.Suspend().ShouldBeFalse();
        c.DomainEvents.OfType<PartySuspendedEvent>().Count().ShouldBe(1);
    }

    [Fact]
    public void Activate_FromSuspended_RestoresActive()
    {
        Party c = NewCompany();
        c.Suspend();

        bool result = c.Activate();

        result.ShouldBeTrue();
        c.Status.ShouldBe(PartyStatus.Active);
        c.DomainEvents.OfType<PartyActivatedEvent>().ShouldHaveSingleItem();
    }

    [Fact]
    public void Activate_AlreadyActive_ReturnsFalse() =>
        NewCompany().Activate().ShouldBeFalse();

    [Fact]
    public void Archive_FromActive_TransitionsToTerminalState()
    {
        Party c = NewCompany();

        c.Archive().ShouldBeTrue();
        c.Status.ShouldBe(PartyStatus.Archived);
        c.DomainEvents.OfType<PartyArchivedEvent>().ShouldHaveSingleItem();
    }

    [Fact]
    public void Archive_FromSuspended_AlsoTerminates()
    {
        Party c = NewCompany();
        c.Suspend();

        c.Archive().ShouldBeTrue();
        c.Status.ShouldBe(PartyStatus.Archived);
    }

    [Fact]
    public void Activate_OnArchived_Throws()
    {
        Party c = NewCompany();
        c.Archive();
        Should.Throw<InvalidOperationException>(() => c.Activate());
    }

    [Fact]
    public void Suspend_OnArchived_Throws()
    {
        Party c = NewCompany();
        c.Archive();
        Should.Throw<InvalidOperationException>(() => c.Suspend());
    }

    // ── PII pseudonymisation (GDPR Art. 17) ───────────────────────

    [Fact]
    public void PseudonymizePersonalData_ReplacesNameAndClearsCollections()
    {
        Party c = NewIndividual();
        c.AddEmail(Guid.NewGuid(), "x@y.com");
        c.AddPhone(Guid.NewGuid(), PhoneKind.Mobile, "+33611223344");
        c.AddAddress(Guid.NewGuid(), AddressKind.Billing, Address.Create("L1", "C", "1000", "BE"));
        c.LinkToUser(Guid.NewGuid());
        c.UpdateIdentity("Jean", website: "https://jean.example.com");

        bool changed = c.PseudonymizePersonalData();

        changed.ShouldBeTrue();
        c.Name.ShouldBe("[deleted]");
        c.Emails.ShouldBeEmpty();
        c.Phones.ShouldBeEmpty();
        c.Addresses.ShouldBeEmpty();
        c.Website.ShouldBeNull();
        c.UserId.ShouldBeNull();
        c.DomainEvents.OfType<PartyPersonalDataPseudonymizedEvent>().ShouldHaveSingleItem();
        c.IntegrationEvents.OfType<PartyPersonalDataPseudonymizedEto>().ShouldHaveSingleItem();
    }

    [Fact]
    public void PseudonymizePersonalData_PreservesAccountingFields()
    {
        var c = Party.Create(
            Guid.NewGuid(), null, PartyKind.Company, "Acme", "EUR",
            taxId: "BE0123456789", registrationNumber: "0123.456.789");
        c.AddExternalMapping(Guid.NewGuid(), "stripe", "cus_1");

        c.PseudonymizePersonalData();

        c.TaxId.ShouldBe("BE0123456789");
        c.RegistrationNumber.ShouldBe("0123.456.789");
        c.ExternalMappings.ShouldHaveSingleItem();
    }

    [Fact]
    public void PseudonymizePersonalData_OnArchived_StillSucceeds()
    {
        Party c = NewIndividual();
        c.AddEmail(Guid.NewGuid(), "x@y.com");
        c.Archive();

        bool changed = c.PseudonymizePersonalData();

        changed.ShouldBeTrue();
        c.Emails.ShouldBeEmpty();
    }

    [Fact]
    public void PseudonymizePersonalData_AlreadyPseudonymised_ReturnsFalse()
    {
        var c = Party.Create(
            Guid.NewGuid(), null, PartyKind.Company, "[deleted]", "EUR");

        c.PseudonymizePersonalData().ShouldBeFalse();
        c.DomainEvents.OfType<PartyPersonalDataPseudonymizedEvent>().ShouldBeEmpty();
    }

    // ── Billing address snapshot ──────────────────────────────────

    [Fact]
    public void GetBillingAddressSnapshot_NoBillingAddress_ReturnsNull() =>
        NewCompany().GetBillingAddressSnapshot().ShouldBeNull();

    [Fact]
    public void GetBillingAddressSnapshot_WithDefaultBilling_BundlesAddressAndTaxId()
    {
        var c = Party.Create(
            Guid.NewGuid(), null, PartyKind.Company, "Acme", "EUR",
            taxId: "BE0123456789");
        c.AddAddress(
            Guid.NewGuid(),
            AddressKind.Billing,
            Address.Create("rue 1", "Brussels", "1000", "BE", companyName: "Acme HQ"));

        BillingAddress? snapshot = c.GetBillingAddressSnapshot();

        snapshot.ShouldNotBeNull();
        snapshot.Line1.ShouldBe("rue 1");
        snapshot.City.ShouldBe("Brussels");
        snapshot.PostalCode.ShouldBe("1000");
        snapshot.Country.ShouldBe("BE");
        snapshot.CompanyName.ShouldBe("Acme HQ");
        snapshot.VatNumber.ShouldBe("BE0123456789");
    }

    [Fact]
    public void GetBillingAddressSnapshot_AddressWithoutCompanyName_FallsBackToContactName()
    {
        var c = Party.Create(
            Guid.NewGuid(), null, PartyKind.Company, "Acme Corp", "EUR");
        c.AddAddress(
            Guid.NewGuid(),
            AddressKind.Billing,
            Address.Create("rue 1", "Brussels", "1000", "BE"));

        BillingAddress? snapshot = c.GetBillingAddressSnapshot();

        snapshot.ShouldNotBeNull();
        snapshot.CompanyName.ShouldBe("Acme Corp");
    }

    [Fact]
    public void GetBillingAddressSnapshot_PrefersDefaultBilling()
    {
        var c = Party.Create(
            Guid.NewGuid(), null, PartyKind.Company, "Acme", "EUR");
        c.AddAddress(
            Guid.NewGuid(),
            AddressKind.Billing,
            Address.Create("old", "Paris", "75000", "FR"));
        var defaultId = Guid.NewGuid();
        c.AddAddress(
            defaultId,
            AddressKind.Billing,
            Address.Create("new", "Brussels", "1000", "BE"),
            isDefault: true);

        BillingAddress? snapshot = c.GetBillingAddressSnapshot();

        snapshot.ShouldNotBeNull();
        snapshot.Line1.ShouldBe("new");
        snapshot.Country.ShouldBe("BE");
    }

    // ── Identity & address updates ────────────────────────────────

    [Fact]
    public void UpdateIdentity_OnActive_AppliesAndRaisesEvent()
    {
        Party c = NewCompany();

        c.UpdateIdentity("New Acme", website: "https://new.acme.com");

        c.Name.ShouldBe("New Acme");
        c.Website.ShouldBe("https://new.acme.com");
        c.DomainEvents.OfType<PartyUpdatedEvent>().ShouldHaveSingleItem();
    }

    [Fact]
    public void UpdateIdentity_OnArchived_Throws()
    {
        Party c = NewCompany();
        c.Archive();
        Should.Throw<InvalidOperationException>(() => c.UpdateIdentity("X"));
    }

    // ── Multi-address ─────────────────────────────────────────────

    [Fact]
    public void AddAddress_FirstOfKind_AutoMarksAsDefault()
    {
        Party c = NewCompany();
        var addressId = Guid.NewGuid();
        var addr = Address.Create("rue 1", "Brussels", "1000", "BE");

        c.AddAddress(addressId, AddressKind.Billing, addr);

        c.Addresses.ShouldHaveSingleItem();
        c.Addresses[0].IsDefault.ShouldBeTrue();
        c.Addresses[0].Kind.ShouldBe(AddressKind.Billing);
        c.Addresses[0].Value.ShouldBe(addr);
        c.DefaultBillingAddress.ShouldBe(c.Addresses[0]);
    }

    [Fact]
    public void AddAddress_SecondOfSameKind_NotDefaultByDefault()
    {
        Party c = NewCompany();
        c.AddAddress(Guid.NewGuid(), AddressKind.Billing, Address.Create("L1", "C", "1000", "BE"));
        var secondId = Guid.NewGuid();
        c.AddAddress(secondId, AddressKind.Billing, Address.Create("L2", "C", "2000", "BE"));

        c.Addresses.Count.ShouldBe(2);
        c.Addresses.Single(a => a.Id == secondId).IsDefault.ShouldBeFalse();
    }

    [Fact]
    public void AddAddress_WithIsDefaultTrue_DemotesPreviousDefault()
    {
        Party c = NewCompany();
        var firstId = Guid.NewGuid();
        c.AddAddress(firstId, AddressKind.Billing, Address.Create("L1", "C", "1000", "BE"));
        var secondId = Guid.NewGuid();
        c.AddAddress(secondId, AddressKind.Billing, Address.Create("L2", "C", "2000", "BE"), isDefault: true);

        c.Addresses.Single(a => a.Id == firstId).IsDefault.ShouldBeFalse();
        c.Addresses.Single(a => a.Id == secondId).IsDefault.ShouldBeTrue();
    }

    [Fact]
    public void AddAddress_DifferentKind_BothCanBeDefault()
    {
        Party c = NewCompany();
        c.AddAddress(Guid.NewGuid(), AddressKind.Billing, Address.Create("B", "C", "1000", "BE"));
        c.AddAddress(Guid.NewGuid(), AddressKind.Shipping, Address.Create("S", "C", "1000", "BE"));

        c.Addresses.Count.ShouldBe(2);
        c.DefaultBillingAddress.ShouldNotBeNull();
        c.DefaultShippingAddress.ShouldNotBeNull();
    }

    [Fact]
    public void AddAddress_StoresLabel()
    {
        Party c = NewCompany();
        c.AddAddress(Guid.NewGuid(), AddressKind.Other,
            Address.Create("L1", "C", "1000", "BE"), label: "HQ");

        c.Addresses[0].Label.ShouldBe("HQ");
    }

    [Fact]
    public void RemoveAddress_KnownId_PromotesAnotherOfSameKindToDefault()
    {
        Party c = NewCompany();
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        c.AddAddress(firstId, AddressKind.Billing, Address.Create("L1", "C", "1000", "BE"));
        c.AddAddress(secondId, AddressKind.Billing, Address.Create("L2", "C", "2000", "BE"));

        c.RemoveAddress(firstId).ShouldBeTrue();

        c.Addresses.ShouldHaveSingleItem();
        c.Addresses[0].Id.ShouldBe(secondId);
        c.Addresses[0].IsDefault.ShouldBeTrue(); // promoted
    }

    [Fact]
    public void RemoveAddress_OnlyDefaultOfKind_LeavesNoDefault()
    {
        Party c = NewCompany();
        var id = Guid.NewGuid();
        c.AddAddress(id, AddressKind.Billing, Address.Create("L1", "C", "1000", "BE"));

        c.RemoveAddress(id).ShouldBeTrue();

        c.Addresses.ShouldBeEmpty();
        c.DefaultBillingAddress.ShouldBeNull();
    }

    [Fact]
    public void RemoveAddress_UnknownId_ReturnsFalse() =>
        NewCompany().RemoveAddress(Guid.NewGuid()).ShouldBeFalse();

    [Fact]
    public void UpdateAddress_KnownId_ReplacesValue()
    {
        Party c = NewCompany();
        var id = Guid.NewGuid();
        c.AddAddress(id, AddressKind.Billing, Address.Create("L1", "C", "1000", "BE"));

        var newAddr = Address.Create("L2", "C", "2000", "BE");
        c.UpdateAddress(id, newAddr, label: "Updated");

        c.Addresses[0].Value.ShouldBe(newAddr);
        c.Addresses[0].Label.ShouldBe("Updated");
    }

    [Fact]
    public void UpdateAddress_UnknownId_Throws() =>
        Should.Throw<InvalidOperationException>(() =>
            NewCompany().UpdateAddress(Guid.NewGuid(), Address.Create("L", "C", "1000", "BE")));

    [Fact]
    public void SetDefaultAddress_ValidId_DemotesPreviousAndPromotesTarget()
    {
        Party c = NewCompany();
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        c.AddAddress(firstId, AddressKind.Billing, Address.Create("L1", "C", "1000", "BE"));
        c.AddAddress(secondId, AddressKind.Billing, Address.Create("L2", "C", "2000", "BE"));

        c.SetDefaultAddress(secondId);

        c.Addresses.Single(a => a.Id == firstId).IsDefault.ShouldBeFalse();
        c.Addresses.Single(a => a.Id == secondId).IsDefault.ShouldBeTrue();
    }

    [Fact]
    public void SetDefaultAddress_AlreadyDefault_NoOp()
    {
        Party c = NewCompany();
        var id = Guid.NewGuid();
        c.AddAddress(id, AddressKind.Billing, Address.Create("L1", "C", "1000", "BE"));

        Should.NotThrow(() => c.SetDefaultAddress(id));
        c.Addresses[0].IsDefault.ShouldBeTrue();
    }

    [Fact]
    public void SetDefaultAddress_UnknownId_Throws() =>
        Should.Throw<InvalidOperationException>(() =>
            NewCompany().SetDefaultAddress(Guid.NewGuid()));

    [Fact]
    public void AddAddress_OnArchived_Throws()
    {
        Party c = NewCompany();
        c.Archive();

        Should.Throw<InvalidOperationException>(() =>
            c.AddAddress(Guid.NewGuid(), AddressKind.Billing, Address.Create("L", "C", "1000", "BE")));
    }

    [Fact]
    public void UpdateTaxIdentity_StoresValues()
    {
        Party c = NewCompany();

        c.UpdateTaxIdentity("BE0123456789", "0123.456.789");

        c.TaxId.ShouldBe("BE0123456789");
        c.RegistrationNumber.ShouldBe("0123.456.789");
        c.DomainEvents.OfType<PartyUpdatedEvent>().ShouldHaveSingleItem();
    }

    // ── Hierarchy ─────────────────────────────────────────────────

    [Fact]
    public void AttachToParent_SameTenant_Succeeds()
    {
        var tenantId = Guid.NewGuid();
        Party child = NewIndividual(tenantId);
        var parentId = PartyId.Create(Guid.NewGuid());

        child.AttachToParent(parentId, parentTenantId: tenantId);

        child.ParentPartyId.ShouldBe(parentId);
        child.DomainEvents.OfType<PartyAttachedToParentEvent>().ShouldHaveSingleItem();
    }

    [Fact]
    public void AttachToParent_HostScope_Succeeds()
    {
        Party child = NewIndividual(tenantId: null);
        var parentId = PartyId.Create(Guid.NewGuid());

        child.AttachToParent(parentId, parentTenantId: null);

        child.ParentPartyId.ShouldBe(parentId);
    }

    [Fact]
    public void AttachToParent_DifferentTenant_Throws()
    {
        Party child = NewIndividual(tenantId: Guid.NewGuid());
        var parentId = PartyId.Create(Guid.NewGuid());

        Should.Throw<InvalidOperationException>(() =>
            child.AttachToParent(parentId, parentTenantId: Guid.NewGuid()));
    }

    [Fact]
    public void AttachToParent_Self_Throws()
    {
        Party c = NewIndividual();
        var selfId = PartyId.Create(c.Id);

        Should.Throw<InvalidOperationException>(() =>
            c.AttachToParent(selfId, parentTenantId: c.TenantId));
    }

    [Fact]
    public void DetachFromParent_WhenAttached_ReturnsTrue()
    {
        Party c = NewIndividual();
        c.AttachToParent(PartyId.Create(Guid.NewGuid()), c.TenantId);

        bool result = c.DetachFromParent();

        result.ShouldBeTrue();
        c.ParentPartyId.ShouldBeNull();
        c.DomainEvents.OfType<PartyDetachedFromParentEvent>().ShouldHaveSingleItem();
    }

    [Fact]
    public void DetachFromParent_WhenNotAttached_ReturnsFalse() =>
        NewIndividual().DetachFromParent().ShouldBeFalse();

    // ── User linkage ──────────────────────────────────────────────

    [Fact]
    public void LinkToUser_OnIndividual_StoresUserId()
    {
        Party c = NewIndividual();
        var userId = Guid.NewGuid();

        c.LinkToUser(userId);

        c.UserId.ShouldBe(userId);
        c.DomainEvents.OfType<PartyLinkedToUserEvent>().ShouldHaveSingleItem();
    }

    [Fact]
    public void LinkToUser_OnCompany_Throws()
    {
        Party c = NewCompany();

        Should.Throw<InvalidOperationException>(() => c.LinkToUser(Guid.NewGuid()));
    }

    [Fact]
    public void LinkToUser_EmptyGuid_Throws()
    {
        Party c = NewIndividual();

        Should.Throw<ArgumentException>(() => c.LinkToUser(Guid.Empty));
    }

    [Fact]
    public void UnlinkFromUser_WhenLinked_ClearsUserId()
    {
        Party c = NewIndividual();
        c.LinkToUser(Guid.NewGuid());

        c.UnlinkFromUser().ShouldBeTrue();
        c.UserId.ShouldBeNull();
    }

    [Fact]
    public void UnlinkFromUser_WhenNotLinked_ReturnsFalse() =>
        NewIndividual().UnlinkFromUser().ShouldBeFalse();

    // ── Avatar ────────────────────────────────────────────────────

    [Fact]
    public void SetAvatar_StoresBlobId_AndRaisesUpdated()
    {
        Party c = NewCompany();
        var blobId = Guid.NewGuid();

        c.SetAvatar(blobId);

        c.AvatarBlobId.ShouldBe(blobId);
        c.DomainEvents.OfType<PartyUpdatedEvent>().ShouldHaveSingleItem();
    }

    [Fact]
    public void SetAvatar_EmptyGuid_Throws() =>
        Should.Throw<ArgumentException>(() => NewCompany().SetAvatar(Guid.Empty));

    [Fact]
    public void SetAvatar_OnArchived_Throws()
    {
        Party c = NewCompany();
        c.Archive();
        Should.Throw<InvalidOperationException>(() => c.SetAvatar(Guid.NewGuid()));
    }

    [Fact]
    public void ClearAvatar_WhenSet_ReturnsTrue_AndClears()
    {
        Party c = NewCompany();
        c.SetAvatar(Guid.NewGuid());

        c.ClearAvatar().ShouldBeTrue();
        c.AvatarBlobId.ShouldBeNull();
    }

    [Fact]
    public void ClearAvatar_WhenNotSet_ReturnsFalse() =>
        NewCompany().ClearAvatar().ShouldBeFalse();

    // ── Roles ─────────────────────────────────────────────────────

    [Fact]
    public void AddRole_NewRole_TogglesFlagAndRaisesEvent()
    {
        Party c = NewCompany();
        c.HasRole(PartyRoles.Supplier).ShouldBeFalse();

        c.AddRole(PartyRoles.Supplier).ShouldBeTrue();

        c.HasRole(PartyRoles.Supplier).ShouldBeTrue();
        c.HasRole(PartyRoles.Customer).ShouldBeTrue(); // not removed
        c.DomainEvents.OfType<PartyRoleAddedEvent>().ShouldHaveSingleItem();
    }

    [Fact]
    public void AddRole_AlreadyPresent_IsIdempotent()
    {
        Party c = NewCompany(); // has Customer

        c.AddRole(PartyRoles.Customer).ShouldBeFalse();
        c.DomainEvents.OfType<PartyRoleAddedEvent>().ShouldBeEmpty();
    }

    [Fact]
    public void AddRole_None_IsNoOp() =>
        NewCompany().AddRole(PartyRoles.None).ShouldBeFalse();

    [Fact]
    public void RemoveRole_KnownRole_ClearsAndRaisesEvent()
    {
        Party c = NewCompany();

        c.RemoveRole(PartyRoles.Customer).ShouldBeTrue();
        c.HasRole(PartyRoles.Customer).ShouldBeFalse();
        c.Roles.ShouldBe(PartyRoles.None);
        c.DomainEvents.OfType<PartyRoleRemovedEvent>().ShouldHaveSingleItem();
    }

    [Fact]
    public void RemoveRole_NotPresent_IsIdempotent() =>
        NewCompany().RemoveRole(PartyRoles.Supplier).ShouldBeFalse();

    [Fact]
    public void Roles_CanCarryMultipleSimultaneously()
    {
        Party c = NewCompany();

        c.AddRole(PartyRoles.Supplier);
        c.AddRole(PartyRoles.Lead);

        c.HasRole(PartyRoles.Customer).ShouldBeTrue();
        c.HasRole(PartyRoles.Supplier).ShouldBeTrue();
        c.HasRole(PartyRoles.Lead).ShouldBeTrue();
        c.HasRole(PartyRoles.Customer | PartyRoles.Supplier).ShouldBeTrue();
    }

    // ── External mappings ─────────────────────────────────────────

    [Fact]
    public void AddExternalMapping_NewProvider_AppendsAndRaisesEvents()
    {
        Party c = NewCompany();

        c.AddExternalMapping(Guid.NewGuid(), PartyExternalProviderNames.Stripe, "cus_123");

        c.ExternalMappings.Count.ShouldBe(1);
        c.FindExternalId("stripe").ShouldBe("cus_123");
        c.DomainEvents.OfType<PartyExternalMappingAddedEvent>().ShouldHaveSingleItem();
        c.IntegrationEvents.OfType<PartyExternalMappingAddedEto>().ShouldHaveSingleItem();
    }

    [Fact]
    public void AddExternalMapping_DuplicateProvider_Throws()
    {
        Party c = NewCompany();
        c.AddExternalMapping(Guid.NewGuid(), "stripe", "cus_1");

        Should.Throw<InvalidOperationException>(() =>
            c.AddExternalMapping(Guid.NewGuid(), "STRIPE", "cus_2"));
    }

    [Fact]
    public void RemoveExternalMapping_KnownProvider_ReturnsTrue()
    {
        Party c = NewCompany();
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
        Party c = NewCompany();
        c.AddExternalMapping(Guid.NewGuid(), "odoo", "42");

        c.FindExternalId("odoo").ShouldBe("42");
        c.FindExternalId("ODOO").ShouldBe("42");
    }

    [Fact]
    public void FindExternalId_UnknownProvider_ReturnsNull() =>
        NewCompany().FindExternalId("stripe").ShouldBeNull();

    // ── Multi-tenancy ─────────────────────────────────────────────

    [Fact]
    public void Party_HostScoped_HasNullTenantId() =>
        Party.Create(Guid.NewGuid(), null, PartyKind.Company, "X", "EUR")
            .TenantId.ShouldBeNull();

    [Fact]
    public void Party_ImplementsIMultiTenant() =>
        typeof(Party).IsAssignableTo(typeof(Granit.Domain.IMultiTenant)).ShouldBeTrue();

    [Fact]
    public void Contact_ExplicitInterface_AllowsTenantIdMutation_ForInterceptor()
    {
        var c = Party.Create(Guid.NewGuid(), null, PartyKind.Company, "X", "EUR");
        var newTenant = Guid.NewGuid();

        ((Granit.Domain.IMultiTenant)c).TenantId = newTenant;

        c.TenantId.ShouldBe(newTenant);
    }

    // ── Tax status (customer-specific tax classification) ─────────

    [Fact]
    public void TaxStatus_Default_IsStandard() =>
        NewCompany().TaxStatus.ShouldBe(TaxStatus.Standard);

    [Fact]
    public void TaxStatus_Default_DoesNotYieldZeroRate() =>
        NewCompany().TaxStatus.YieldsZeroRate.ShouldBeFalse();

    [Fact]
    public void SetTaxStatus_FromStandardToExempt_StoresIt()
    {
        Party c = NewCompany();
        var exempt = TaxStatus.Create(isExempt: true);

        bool changed = c.SetTaxStatus(exempt);

        changed.ShouldBeTrue();
        c.TaxStatus.IsExempt.ShouldBeTrue();
        c.TaxStatus.YieldsZeroRate.ShouldBeTrue();
    }

    [Fact]
    public void SetTaxStatus_ReverseChargeRequiresVatin() =>
        Should.Throw<ArgumentException>(() =>
            TaxStatus.Create(reverseCharge: true, vatin: null));

    [Fact]
    public void SetTaxStatus_ReverseChargeWithVatin_StoresIt()
    {
        Party c = NewCompany();
        var rc = TaxStatus.Create(reverseCharge: true, vatin: "BE0123456789");

        c.SetTaxStatus(rc);

        c.TaxStatus.ReverseCharge.ShouldBeTrue();
        c.TaxStatus.Vatin.ShouldBe("BE0123456789");
        c.TaxStatus.YieldsZeroRate.ShouldBeTrue();
    }

    [Fact]
    public void SetTaxStatus_SameValue_ReturnsFalse()
    {
        Party c = NewCompany();
        c.SetTaxStatus(TaxStatus.Create(isExempt: true));

        c.SetTaxStatus(TaxStatus.Create(isExempt: true)).ShouldBeFalse();
    }

    [Fact]
    public void SetTaxStatus_Null_FallsBackToStandard()
    {
        Party c = NewCompany();
        c.SetTaxStatus(TaxStatus.Create(isExempt: true));

        c.SetTaxStatus(null).ShouldBeTrue();
        c.TaxStatus.ShouldBe(TaxStatus.Standard);
        c.TaxStatus.YieldsZeroRate.ShouldBeFalse();
    }

    // ── Metadata (Stripe-style customer.metadata) ─────────────────

    [Fact]
    public void Metadata_DefaultsToEmpty() =>
        NewCompany().GetMetadata().ShouldBeEmpty();

    [Fact]
    public void ReplaceMetadata_StoresEntries()
    {
        Party c = NewCompany();
        var entries = new Dictionary<string, string>
        {
            ["segment"] = "enterprise",
            ["sales_rep_id"] = "alice",
        };

        c.ReplaceMetadata(entries);

        c.GetMetadata().Count.ShouldBe(2);
        c.GetMetadataValue("segment").ShouldBe("enterprise");
        c.GetMetadataValue("sales_rep_id").ShouldBe("alice");
    }

    [Fact]
    public void ReplaceMetadata_EmptyDictionary_ClearsMetadata()
    {
        Party c = NewCompany();
        c.ReplaceMetadata(new Dictionary<string, string> { ["k"] = "v" });

        c.ReplaceMetadata(new Dictionary<string, string>());

        c.MetadataJson.ShouldBeNull();
        c.GetMetadata().ShouldBeEmpty();
    }

    [Fact]
    public void ReplaceMetadata_OnArchived_Throws()
    {
        Party c = NewCompany();
        c.Archive();
        Should.Throw<InvalidOperationException>(() =>
            c.ReplaceMetadata(new Dictionary<string, string> { ["k"] = "v" }));
    }

    [Fact]
    public void SetMetadataValue_AddsAndOverwrites()
    {
        Party c = NewCompany();
        c.SetMetadataValue("segment", "smb");
        c.SetMetadataValue("segment", "enterprise");
        c.GetMetadataValue("segment").ShouldBe("enterprise");
    }

    // ── Internal notes ────────────────────────────────────────────

    [Fact]
    public void InternalNotes_DefaultsToNull() =>
        NewCompany().InternalNotes.ShouldBeNull();

    [Fact]
    public void Create_WithInternalNotes_StoresThem()
    {
        var c = Party.Create(
            Guid.NewGuid(), null, PartyKind.Company, "Acme", "EUR",
            internalNotes: "VIP — escalate to alice@acme.com");

        c.InternalNotes.ShouldBe("VIP — escalate to alice@acme.com");
    }

    [Fact]
    public void Create_InternalNotesTooLong_Throws() =>
        Should.Throw<ArgumentException>(() =>
            Party.Create(Guid.NewGuid(), null, PartyKind.Company, "Acme", "EUR",
                internalNotes: new string('x', 8_001)));

    [Fact]
    public void SetInternalNotes_StoresValue()
    {
        Party c = NewCompany();
        c.SetInternalNotes("Account on hold pending compliance review.");
        c.InternalNotes.ShouldBe("Account on hold pending compliance review.");
    }

    [Fact]
    public void SetInternalNotes_EmptyOrWhitespace_ClearsValue()
    {
        Party c = NewCompany();
        c.SetInternalNotes("note");

        c.SetInternalNotes("   ");
        c.InternalNotes.ShouldBeNull();
    }

    [Fact]
    public void SetInternalNotes_TooLong_Throws() =>
        Should.Throw<ArgumentException>(() =>
            NewCompany().SetInternalNotes(new string('x', Party.MaxInternalNotesLength + 1)));

    [Fact]
    public void UpdateIdentity_UpdatesInternalNotesAlongsideOtherFields()
    {
        Party c = NewCompany();
        c.UpdateIdentity("New Name", website: "https://x.com", internalNotes: "moved to enterprise plan");
        c.Name.ShouldBe("New Name");
        c.Website.ShouldBe("https://x.com");
        c.InternalNotes.ShouldBe("moved to enterprise plan");
    }
}
