using Granit.Contacts.Domain;
using Granit.Contacts.Domain.ValueObjects;
using Granit.Contacts.EntityFrameworkCore.Internal;
using Granit.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Contacts.EntityFrameworkCore.Tests.Internal;

[Collection(ContactsDbSerialGroup.Name)]
public sealed class EfContactStoreTests : IAsyncDisposable
{
    private readonly TestFactory _factory;
    private readonly EfContactStore _store;

    public EfContactStoreTests()
    {
        DbContextOptions<ContactsDbContext> options = new DbContextOptionsBuilder<ContactsDbContext>()
            .UseInMemoryDatabase($"contacts-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _factory = new TestFactory(options);
        _store = new EfContactStore(_factory, _factory.Tenant);
    }

    public async ValueTask DisposeAsync()
    {
        await using ContactsDbContext db = await _factory.CreateDbContextAsync();
        await db.Database.EnsureDeletedAsync();
    }

    private static Contact NewContact(
        ContactKind kind = ContactKind.Company,
        string name = "Acme",
        ContactRoles roles = ContactRoles.Customer) =>
        Contact.Create(Guid.NewGuid(), null, kind, name, "EUR", roles);

    [Fact]
    public async Task AddAsync_ThenGetByIdAsync_RoundTrips()
    {
        Contact c = NewContact();
        await ((IContactWriter)_store).AddAsync(c, TestContext.Current.CancellationToken);

        Contact? loaded = await _store.GetByIdAsync(
            ContactId.Create(c.Id), TestContext.Current.CancellationToken);

        loaded.ShouldNotBeNull();
        loaded.Name.ShouldBe("Acme");
        loaded.Kind.ShouldBe(ContactKind.Company);
        loaded.Status.ShouldBe(ContactStatus.Active);
        loaded.HasRole(ContactRoles.Customer).ShouldBeTrue();
    }

    [Fact]
    public async Task GetByIdAsync_LoadsAllChildCollections()
    {
        Contact c = NewContact();
        c.AddAddress(Guid.NewGuid(), AddressKind.Billing, Address.Create("L1", "C", "1000", "BE"));
        c.AddEmail(Guid.NewGuid(), "billing@acme.com", isPrimary: true, label: "billing");
        c.AddPhone(Guid.NewGuid(), PhoneKind.Office, "+3221234567", isPrimary: true);
        c.AddExternalMapping(Guid.NewGuid(), ContactExternalProviderNames.Stripe, "cus_1");
        await ((IContactWriter)_store).AddAsync(c, TestContext.Current.CancellationToken);

        Contact? loaded = await _store.GetByIdAsync(
            ContactId.Create(c.Id), TestContext.Current.CancellationToken);

        loaded.ShouldNotBeNull();
        loaded.Addresses.ShouldHaveSingleItem();
        loaded.Emails.ShouldHaveSingleItem();
        loaded.Phones.ShouldHaveSingleItem();
        loaded.ExternalMappings.ShouldHaveSingleItem();
        loaded.PrimaryEmail?.Address.ShouldBe("billing@acme.com");
        loaded.PrimaryPhone?.Number.ShouldBe("+3221234567");
        loaded.PrimaryPhone?.Kind.ShouldBe(PhoneKind.Office);
        loaded.DefaultBillingAddress.ShouldNotBeNull();
        loaded.FindExternalId("stripe").ShouldBe("cus_1");
    }

    [Fact]
    public async Task GetByIdAsync_Missing_ReturnsNull() =>
        (await _store.GetByIdAsync(ContactId.Create(Guid.NewGuid()),
            TestContext.Current.CancellationToken)).ShouldBeNull();

    [Fact]
    public async Task GetByExternalIdAsync_Match_ReturnsContact()
    {
        Contact c = NewContact();
        c.AddExternalMapping(Guid.NewGuid(), "stripe", "cus_match");
        await ((IContactWriter)_store).AddAsync(c, TestContext.Current.CancellationToken);

        Contact? hit = await _store.GetByExternalIdAsync(
            "stripe", "cus_match", TestContext.Current.CancellationToken);

        hit.ShouldNotBeNull();
        hit.Id.ShouldBe(c.Id);
    }

    [Fact]
    public async Task GetByExternalIdAsync_NoMatch_ReturnsNull()
    {
        Contact c = NewContact();
        c.AddExternalMapping(Guid.NewGuid(), "stripe", "cus_match");
        await ((IContactWriter)_store).AddAsync(c, TestContext.Current.CancellationToken);

        (await _store.GetByExternalIdAsync("stripe", "cus_nope",
            TestContext.Current.CancellationToken)).ShouldBeNull();
    }

    [Theory]
    [InlineData("", "x")]
    [InlineData(" ", "x")]
    [InlineData("stripe", "")]
    [InlineData("stripe", " ")]
    public async Task GetByExternalIdAsync_BlankArgs_Throws(string providerName, string externalId) =>
        await Should.ThrowAsync<ArgumentException>(() =>
            _store.GetByExternalIdAsync(providerName, externalId, TestContext.Current.CancellationToken));

    [Fact]
    public async Task GetByUserIdAsync_LinkedIndividual_ReturnsContact()
    {
        Contact c = NewContact(ContactKind.Individual, name: "Jean");
        var userId = Guid.NewGuid();
        c.LinkToUser(userId);
        await ((IContactWriter)_store).AddAsync(c, TestContext.Current.CancellationToken);

        Contact? hit = await _store.GetByUserIdAsync(userId, TestContext.Current.CancellationToken);

        hit.ShouldNotBeNull();
        hit.Id.ShouldBe(c.Id);
    }

    [Fact]
    public async Task GetByUserIdAsync_Missing_ReturnsNull() =>
        (await _store.GetByUserIdAsync(Guid.NewGuid(),
            TestContext.Current.CancellationToken)).ShouldBeNull();

    [Fact]
    public async Task GetByUserIdAsync_EmptyGuid_Throws() =>
        await Should.ThrowAsync<ArgumentException>(() =>
            _store.GetByUserIdAsync(Guid.Empty, TestContext.Current.CancellationToken));

    [Fact]
    public async Task ListAsync_ReturnsAllInScope()
    {
        await ((IContactWriter)_store).AddAsync(NewContact(name: "A"), TestContext.Current.CancellationToken);
        await ((IContactWriter)_store).AddAsync(NewContact(name: "B"), TestContext.Current.CancellationToken);

        IReadOnlyList<Contact> result = await _store.ListAsync(TestContext.Current.CancellationToken);

        result.Count.ShouldBe(2);
    }

    [Fact]
    public async Task ListByRoleAsync_FiltersOnFlag()
    {
        Contact customer = NewContact(name: "Cust", roles: ContactRoles.Customer);
        Contact supplier = NewContact(name: "Supp", roles: ContactRoles.Supplier);
        Contact both = NewContact(name: "Both", roles: ContactRoles.Customer | ContactRoles.Supplier);
        await ((IContactWriter)_store).AddAsync(customer, TestContext.Current.CancellationToken);
        await ((IContactWriter)_store).AddAsync(supplier, TestContext.Current.CancellationToken);
        await ((IContactWriter)_store).AddAsync(both, TestContext.Current.CancellationToken);

        IReadOnlyList<Contact> customers = await _store.ListByRoleAsync(
            ContactRoles.Customer, TestContext.Current.CancellationToken);
        IReadOnlyList<Contact> suppliers = await _store.ListByRoleAsync(
            ContactRoles.Supplier, TestContext.Current.CancellationToken);

        customers.Select(c => c.Name).ShouldBe(["Cust", "Both"], ignoreOrder: true);
        suppliers.Select(c => c.Name).ShouldBe(["Supp", "Both"], ignoreOrder: true);
    }

    [Fact]
    public async Task UpdateAsync_PersistsLifecycleTransition()
    {
        Contact c = NewContact();
        await ((IContactWriter)_store).AddAsync(c, TestContext.Current.CancellationToken);

        c.Suspend("test");
        await ((IContactWriter)_store).UpdateAsync(c, TestContext.Current.CancellationToken);

        Contact? loaded = await _store.GetByIdAsync(
            ContactId.Create(c.Id), TestContext.Current.CancellationToken);
        loaded?.Status.ShouldBe(ContactStatus.Suspended);
    }

    [Fact]
    public async Task UpdateAsync_PersistsAddedChildren()
    {
        Contact c = NewContact();
        await ((IContactWriter)_store).AddAsync(c, TestContext.Current.CancellationToken);

        c.AddAddress(Guid.NewGuid(), AddressKind.Billing, Address.Create("L1", "C", "1000", "BE"));
        c.AddEmail(Guid.NewGuid(), "new@acme.com");
        c.AddPhone(Guid.NewGuid(), PhoneKind.Mobile, "+1");
        c.AddExternalMapping(Guid.NewGuid(), "stripe", "cus_added");
        await ((IContactWriter)_store).UpdateAsync(c, TestContext.Current.CancellationToken);

        Contact? loaded = await _store.GetByIdAsync(
            ContactId.Create(c.Id), TestContext.Current.CancellationToken);
        loaded.ShouldNotBeNull();
        loaded.Addresses.ShouldHaveSingleItem();
        loaded.Emails.ShouldHaveSingleItem();
        loaded.Phones.ShouldHaveSingleItem();
        loaded.FindExternalId("stripe").ShouldBe("cus_added");
    }

    [Fact]
    public async Task UpdateAsync_PersistsRoleAdditions()
    {
        Contact c = NewContact();
        await ((IContactWriter)_store).AddAsync(c, TestContext.Current.CancellationToken);

        c.AddRole(ContactRoles.Supplier);
        await ((IContactWriter)_store).UpdateAsync(c, TestContext.Current.CancellationToken);

        Contact? loaded = await _store.GetByIdAsync(
            ContactId.Create(c.Id), TestContext.Current.CancellationToken);
        loaded.ShouldNotBeNull();
        loaded.HasRole(ContactRoles.Customer).ShouldBeTrue();
        loaded.HasRole(ContactRoles.Supplier).ShouldBeTrue();
    }

    /// <summary>
    /// Test factory that omits <c>ICurrentTenant</c> for the DbContext to bypass the
    /// multi-tenant query filter — sidesteps the AsyncLocal flakiness of <c>DataFilter</c>
    /// under parallel test execution.
    /// </summary>
    private sealed class TestFactory(DbContextOptions<ContactsDbContext> options)
        : IDbContextFactory<ContactsDbContext>
    {
        public ICurrentTenant Tenant { get; } = Substitute.For<ICurrentTenant>();

        public ContactsDbContext CreateDbContext() => new(options);

        public Task<ContactsDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new ContactsDbContext(options));
    }
}

[CollectionDefinition(ContactsDbSerialGroup.Name, DisableParallelization = true)]
public sealed class ContactsDbSerialGroup
{
    public const string Name = "Contacts-Db-serial";
}
