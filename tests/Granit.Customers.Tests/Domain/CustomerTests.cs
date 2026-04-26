using Granit.Customers.Domain;
using Granit.Customers.Domain.ValueObjects;
using Granit.Customers.Events;
using Shouldly;
using Xunit;

namespace Granit.Customers.Tests.Domain;

public sealed class CustomerTests
{
    private static Customer NewActive(
        Guid? tenantId = null,
        string legalName = "Acme Corp",
        string currency = "EUR")
    => Customer.Create(Guid.NewGuid(), tenantId, legalName, currency);

    // ── Factory ────────────────────────────────────────────────────

    [Fact]
    public void Create_WithMinimumArgs_StartsActive_AndCapturesIdentity()
    {
        var id = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        var c = Customer.Create(id, tenantId, "Acme Corp", "eur");

        c.Id.ShouldBe(id);
        c.TenantId.ShouldBe(tenantId);
        c.LegalName.ShouldBe("Acme Corp");
        c.DefaultCurrency.ShouldBe("EUR"); // upper-cased
        c.Status.ShouldBe(CustomerStatus.Active);
        c.Timezone.ShouldBe("UTC");
        c.Email.ShouldBeNull();
        c.BillingAddress.ShouldBeNull();
        c.ExternalMappings.ShouldBeEmpty();
    }

    [Fact]
    public void Create_HostScoped_NullTenantId_IsAllowed()
    {
        var c = Customer.Create(Guid.NewGuid(), tenantId: null, "Tenant XYZ", "USD");

        c.TenantId.ShouldBeNull();
    }

    [Fact]
    public void Create_PreservesProvidedTimezone()
    {
        var c = Customer.Create(
            Guid.NewGuid(), null, "X", "EUR", timezone: "Europe/Brussels");

        c.Timezone.ShouldBe("Europe/Brussels");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Create_BlankLegalName_Throws(string? name) =>
        Should.Throw<ArgumentException>(() =>
            Customer.Create(Guid.NewGuid(), null, name!, "EUR"));

    [Theory]
    [InlineData("EU")]      // 2 chars
    [InlineData("EURO")]    // 4 chars
    public void Create_InvalidCurrencyLength_Throws(string currency) =>
        Should.Throw<ArgumentException>(() =>
            Customer.Create(Guid.NewGuid(), null, "X", currency));

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Create_BlankCurrency_Throws(string currency) =>
        Should.Throw<ArgumentException>(() =>
            Customer.Create(Guid.NewGuid(), null, "X", currency));

    [Fact]
    public void Create_RaisesCustomerCreatedDomainAndIntegrationEvents()
    {
        Customer c = NewActive(tenantId: Guid.NewGuid());

        c.DomainEvents.OfType<CustomerCreatedEvent>().ShouldHaveSingleItem();
        c.IntegrationEvents.OfType<CustomerCreatedEto>().ShouldHaveSingleItem();
    }

    // ── Lifecycle: Activate / Suspend / Archive ────────────────────

    [Fact]
    public void Suspend_FromActive_TransitionsAndRaisesEvents()
    {
        Customer c = NewActive();

        bool result = c.Suspend(reason: "unpaid balance");

        result.ShouldBeTrue();
        c.Status.ShouldBe(CustomerStatus.Suspended);
        c.DomainEvents.OfType<CustomerSuspendedEvent>().ShouldHaveSingleItem()
            .Reason.ShouldBe("unpaid balance");
        c.IntegrationEvents.OfType<CustomerSuspendedEto>().ShouldHaveSingleItem();
    }

    [Fact]
    public void Suspend_AlreadySuspended_IsIdempotent_ReturnsFalse()
    {
        Customer c = NewActive();
        c.Suspend();
        c.DomainEvents.OfType<CustomerSuspendedEvent>().Count().ShouldBe(1);

        bool result = c.Suspend();

        result.ShouldBeFalse();
        c.DomainEvents.OfType<CustomerSuspendedEvent>().Count().ShouldBe(1);
    }

    [Fact]
    public void Activate_FromSuspended_RestoresActiveAndRaisesEvents()
    {
        Customer c = NewActive();
        c.Suspend();

        bool result = c.Activate();

        result.ShouldBeTrue();
        c.Status.ShouldBe(CustomerStatus.Active);
        c.DomainEvents.OfType<CustomerActivatedEvent>().ShouldHaveSingleItem();
        c.IntegrationEvents.OfType<CustomerActivatedEto>().ShouldHaveSingleItem();
    }

    [Fact]
    public void Activate_AlreadyActive_IsIdempotent_ReturnsFalse()
    {
        Customer c = NewActive();

        bool result = c.Activate();

        result.ShouldBeFalse();
        c.DomainEvents.OfType<CustomerActivatedEvent>().ShouldBeEmpty();
    }

    [Fact]
    public void Archive_FromActive_TransitionsAndRaisesEvents()
    {
        Customer c = NewActive();

        bool result = c.Archive();

        result.ShouldBeTrue();
        c.Status.ShouldBe(CustomerStatus.Archived);
        c.DomainEvents.OfType<CustomerArchivedEvent>().ShouldHaveSingleItem();
        c.IntegrationEvents.OfType<CustomerArchivedEto>().ShouldHaveSingleItem();
    }

    [Fact]
    public void Archive_FromSuspended_AlsoTerminates()
    {
        Customer c = NewActive();
        c.Suspend();

        c.Archive().ShouldBeTrue();
        c.Status.ShouldBe(CustomerStatus.Archived);
    }

    [Fact]
    public void Archive_AlreadyArchived_IsIdempotent_ReturnsFalse()
    {
        Customer c = NewActive();
        c.Archive();

        c.Archive().ShouldBeFalse();
    }

    [Fact]
    public void Activate_OnArchivedCustomer_Throws()
    {
        Customer c = NewActive();
        c.Archive();

        Should.Throw<InvalidOperationException>(() => c.Activate());
    }

    [Fact]
    public void Suspend_OnArchivedCustomer_Throws()
    {
        Customer c = NewActive();
        c.Archive();

        Should.Throw<InvalidOperationException>(() => c.Suspend());
    }

    // ── Identity updates ───────────────────────────────────────────

    [Fact]
    public void UpdateContact_OnActiveCustomer_UpdatesAndRaisesEvent()
    {
        Customer c = NewActive();

        c.UpdateContact("New Name", email: "billing@new.example");

        c.LegalName.ShouldBe("New Name");
        c.Email.ShouldBe("billing@new.example");
        c.DomainEvents.OfType<CustomerUpdatedEvent>().ShouldHaveSingleItem();
    }

    [Fact]
    public void UpdateContact_BlankLegalName_Throws()
    {
        Customer c = NewActive();

        Should.Throw<ArgumentException>(() => c.UpdateContact("  "));
    }

    [Fact]
    public void UpdateContact_OnArchivedCustomer_Throws()
    {
        Customer c = NewActive();
        c.Archive();

        Should.Throw<InvalidOperationException>(() => c.UpdateContact("X"));
    }

    [Fact]
    public void UpdateBillingAddress_AssignsAndRaisesEvent()
    {
        Customer c = NewActive();
        var addr = BillingAddress.Create("rue de la Loi 16", "Brussels", "1000", "BE");

        c.UpdateBillingAddress(addr);

        c.BillingAddress.ShouldBe(addr);
        c.DomainEvents.OfType<CustomerUpdatedEvent>().ShouldHaveSingleItem();
    }

    [Fact]
    public void UpdateBillingAddress_Null_ClearsExisting()
    {
        Customer c = NewActive();
        c.UpdateBillingAddress(BillingAddress.Create("L1", "C", "1000", "BE"));

        c.UpdateBillingAddress(null);

        c.BillingAddress.ShouldBeNull();
    }

    // ── External mappings ──────────────────────────────────────────

    [Fact]
    public void AddExternalMapping_NewProvider_AppendsAndRaisesEvents()
    {
        Customer c = NewActive();

        c.AddExternalMapping(Guid.NewGuid(), CustomerExternalProviderNames.Stripe, "cus_123");

        c.ExternalMappings.Count.ShouldBe(1);
        c.ExternalMappings[0].ProviderName.ShouldBe("stripe");
        c.ExternalMappings[0].ExternalId.ShouldBe("cus_123");
        c.DomainEvents.OfType<CustomerExternalMappingAddedEvent>().ShouldHaveSingleItem();
        c.IntegrationEvents.OfType<CustomerExternalMappingAddedEto>().ShouldHaveSingleItem();
    }

    [Fact]
    public void AddExternalMapping_DuplicateProvider_Throws()
    {
        Customer c = NewActive();
        c.AddExternalMapping(Guid.NewGuid(), "stripe", "cus_1");

        Should.Throw<InvalidOperationException>(() =>
            c.AddExternalMapping(Guid.NewGuid(), "stripe", "cus_2"));
    }

    [Fact]
    public void AddExternalMapping_DuplicateProvider_CaseInsensitive_Throws()
    {
        Customer c = NewActive();
        c.AddExternalMapping(Guid.NewGuid(), "stripe", "cus_1");

        Should.Throw<InvalidOperationException>(() =>
            c.AddExternalMapping(Guid.NewGuid(), "STRIPE", "cus_2"));
    }

    [Fact]
    public void AddExternalMapping_DifferentProviders_AreAllStored()
    {
        Customer c = NewActive();

        c.AddExternalMapping(Guid.NewGuid(), CustomerExternalProviderNames.Stripe, "cus_S");
        c.AddExternalMapping(Guid.NewGuid(), CustomerExternalProviderNames.Mollie, "cst_M");
        c.AddExternalMapping(Guid.NewGuid(), CustomerExternalProviderNames.Odoo, "42");

        c.ExternalMappings.Count.ShouldBe(3);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void AddExternalMapping_BlankProviderName_Throws(string providerName)
    {
        Customer c = NewActive();

        Should.Throw<ArgumentException>(() =>
            c.AddExternalMapping(Guid.NewGuid(), providerName, "x"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void AddExternalMapping_BlankExternalId_Throws(string externalId)
    {
        Customer c = NewActive();

        Should.Throw<ArgumentException>(() =>
            c.AddExternalMapping(Guid.NewGuid(), "stripe", externalId));
    }

    [Fact]
    public void AddExternalMapping_OnArchivedCustomer_Throws()
    {
        Customer c = NewActive();
        c.Archive();

        Should.Throw<InvalidOperationException>(() =>
            c.AddExternalMapping(Guid.NewGuid(), "stripe", "cus_1"));
    }

    [Fact]
    public void RemoveExternalMapping_KnownProvider_ReturnsTrue()
    {
        Customer c = NewActive();
        c.AddExternalMapping(Guid.NewGuid(), "stripe", "cus_1");

        bool removed = c.RemoveExternalMapping("stripe");

        removed.ShouldBeTrue();
        c.ExternalMappings.ShouldBeEmpty();
    }

    [Fact]
    public void RemoveExternalMapping_CaseInsensitive_Removes()
    {
        Customer c = NewActive();
        c.AddExternalMapping(Guid.NewGuid(), "stripe", "cus_1");

        c.RemoveExternalMapping("STRIPE").ShouldBeTrue();
    }

    [Fact]
    public void RemoveExternalMapping_UnknownProvider_ReturnsFalse()
    {
        Customer c = NewActive();

        c.RemoveExternalMapping("stripe").ShouldBeFalse();
    }

    [Fact]
    public void FindExternalId_KnownProvider_ReturnsValue()
    {
        Customer c = NewActive();
        c.AddExternalMapping(Guid.NewGuid(), "odoo", "42");

        c.FindExternalId("odoo").ShouldBe("42");
        c.FindExternalId("ODOO").ShouldBe("42"); // case-insensitive
    }

    [Fact]
    public void FindExternalId_UnknownProvider_ReturnsNull() =>
        NewActive().FindExternalId("stripe").ShouldBeNull();

    // ── Tenant scope (dual-use) ────────────────────────────────────

    [Fact]
    public void Customer_HostScoped_HasNullTenantId() =>
        Customer.Create(Guid.NewGuid(), null, "Acme", "EUR").TenantId.ShouldBeNull();

    [Fact]
    public void Customer_TenantScoped_PreservesTenantId()
    {
        var tenantId = Guid.NewGuid();

        var c = Customer.Create(Guid.NewGuid(), tenantId, "Acme", "EUR");

        c.TenantId.ShouldBe(tenantId);
    }

    [Fact]
    public void Customer_ImplementsIMultiTenant() =>
        typeof(Customer).IsAssignableTo(typeof(Granit.Domain.IMultiTenant)).ShouldBeTrue();

    [Fact]
    public void Customer_ExplicitInterface_AllowsTenantIdMutation_ForInterceptor()
    {
        // Interceptors set IMultiTenant.TenantId during materialisation.
        var c = Customer.Create(Guid.NewGuid(), null, "X", "EUR");
        var newTenant = Guid.NewGuid();

        ((Granit.Domain.IMultiTenant)c).TenantId = newTenant;

        c.TenantId.ShouldBe(newTenant);
    }
}
