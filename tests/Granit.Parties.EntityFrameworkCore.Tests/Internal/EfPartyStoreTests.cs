using Granit.MultiTenancy;
using Granit.Parties.Domain;
using Granit.Parties.Domain.ValueObjects;
using Granit.Parties.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Parties.EntityFrameworkCore.Tests.Internal;

[Collection(ContactsDbSerialGroup.Name)]
public sealed class EfContactStoreTests : IAsyncDisposable
{
    private readonly TestFactory _factory;
    private readonly EfPartyStore _store;

    public EfContactStoreTests()
    {
        DbContextOptions<PartiesDbContext> options = new DbContextOptionsBuilder<PartiesDbContext>()
            .UseInMemoryDatabase($"contacts-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _factory = new TestFactory(options);
        _store = new EfPartyStore(_factory, _factory.Tenant, StubMeterFactory.CreatePartiesMetrics());
    }

    public async ValueTask DisposeAsync()
    {
        await using PartiesDbContext db = await _factory.CreateDbContextAsync();
        await db.Database.EnsureDeletedAsync();
    }

    private static Party NewContact(
        PartyKind kind = PartyKind.Company,
        string name = "Acme",
        PartyRoles roles = PartyRoles.Customer) =>
        Party.Create(Guid.NewGuid(), null, kind, name, "EUR", roles);

    [Fact]
    public async Task AddAsync_ThenGetByIdAsync_RoundTrips()
    {
        Party c = NewContact();
        await ((IPartyWriter)_store).AddAsync(c, TestContext.Current.CancellationToken);

        Party? loaded = await _store.GetByIdAsync(
            PartyId.Create(c.Id), TestContext.Current.CancellationToken);

        loaded.ShouldNotBeNull();
        loaded.Name.ShouldBe("Acme");
        loaded.Kind.ShouldBe(PartyKind.Company);
        loaded.Status.ShouldBe(PartyStatus.Active);
        loaded.HasRole(PartyRoles.Customer).ShouldBeTrue();
    }

    [Fact]
    public async Task GetByIdAsync_LoadsAllChildCollections()
    {
        Party c = NewContact();
        c.AddAddress(Guid.NewGuid(), AddressKind.Billing, Address.Create("L1", "C", "1000", "BE"));
        c.AddEmail(Guid.NewGuid(), "billing@acme.com", isPrimary: true, label: "billing");
        c.AddPhone(Guid.NewGuid(), PhoneKind.Work, "+3221234567", isPrimary: true);
        c.AddExternalMapping(Guid.NewGuid(), PartyExternalProviderNames.Stripe, "cus_1");
        await ((IPartyWriter)_store).AddAsync(c, TestContext.Current.CancellationToken);

        Party? loaded = await _store.GetByIdAsync(
            PartyId.Create(c.Id), TestContext.Current.CancellationToken);

        loaded.ShouldNotBeNull();
        loaded.Addresses.ShouldHaveSingleItem();
        loaded.Emails.ShouldHaveSingleItem();
        loaded.Phones.ShouldHaveSingleItem();
        loaded.ExternalMappings.ShouldHaveSingleItem();
        loaded.PrimaryEmail?.Address.ShouldBe("billing@acme.com");
        loaded.PrimaryPhone?.Number.ShouldBe("+3221234567");
        loaded.PrimaryPhone?.Kind.ShouldBe(PhoneKind.Work);
        loaded.DefaultBillingAddress.ShouldNotBeNull();
        loaded.FindExternalId("stripe").ShouldBe("cus_1");
    }

    [Fact]
    public async Task GetByIdAsync_Missing_ReturnsNull() =>
        (await _store.GetByIdAsync(PartyId.Create(Guid.NewGuid()),
            TestContext.Current.CancellationToken)).ShouldBeNull();

    [Fact]
    public async Task GetByExternalIdAsync_Match_ReturnsContact()
    {
        Party c = NewContact();
        c.AddExternalMapping(Guid.NewGuid(), "stripe", "cus_match");
        await ((IPartyWriter)_store).AddAsync(c, TestContext.Current.CancellationToken);

        Party? hit = await _store.GetByExternalIdAsync(
            "stripe", "cus_match", TestContext.Current.CancellationToken);

        hit.ShouldNotBeNull();
        hit.Id.ShouldBe(c.Id);
    }

    [Fact]
    public async Task GetByExternalIdAsync_NoMatch_ReturnsNull()
    {
        Party c = NewContact();
        c.AddExternalMapping(Guid.NewGuid(), "stripe", "cus_match");
        await ((IPartyWriter)_store).AddAsync(c, TestContext.Current.CancellationToken);

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
        Party c = NewContact(PartyKind.Individual, name: "Jean");
        var userId = Guid.NewGuid();
        c.LinkToUser(userId);
        await ((IPartyWriter)_store).AddAsync(c, TestContext.Current.CancellationToken);

        Party? hit = await _store.GetByUserIdAsync(userId, TestContext.Current.CancellationToken);

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
        await ((IPartyWriter)_store).AddAsync(NewContact(name: "A"), TestContext.Current.CancellationToken);
        await ((IPartyWriter)_store).AddAsync(NewContact(name: "B"), TestContext.Current.CancellationToken);

        IReadOnlyList<Party> result = await _store.ListAsync(TestContext.Current.CancellationToken);

        result.Count.ShouldBe(2);
    }

    [Fact]
    public async Task ListByRoleAsync_FiltersOnFlag()
    {
        Party customer = NewContact(name: "Cust", roles: PartyRoles.Customer);
        Party supplier = NewContact(name: "Supp", roles: PartyRoles.Supplier);
        Party both = NewContact(name: "Both", roles: PartyRoles.Customer | PartyRoles.Supplier);
        await ((IPartyWriter)_store).AddAsync(customer, TestContext.Current.CancellationToken);
        await ((IPartyWriter)_store).AddAsync(supplier, TestContext.Current.CancellationToken);
        await ((IPartyWriter)_store).AddAsync(both, TestContext.Current.CancellationToken);

        IReadOnlyList<Party> customers = await _store.ListByRoleAsync(
            PartyRoles.Customer, TestContext.Current.CancellationToken);
        IReadOnlyList<Party> suppliers = await _store.ListByRoleAsync(
            PartyRoles.Supplier, TestContext.Current.CancellationToken);

        customers.Select(c => c.Name).ShouldBe(["Cust", "Both"], ignoreOrder: true);
        suppliers.Select(c => c.Name).ShouldBe(["Supp", "Both"], ignoreOrder: true);
    }

    [Fact]
    public async Task UpdateAsync_PersistsLifecycleTransition()
    {
        Party c = NewContact();
        await ((IPartyWriter)_store).AddAsync(c, TestContext.Current.CancellationToken);

        c.Suspend("test");
        await ((IPartyWriter)_store).UpdateAsync(c, TestContext.Current.CancellationToken);

        Party? loaded = await _store.GetByIdAsync(
            PartyId.Create(c.Id), TestContext.Current.CancellationToken);
        loaded?.Status.ShouldBe(PartyStatus.Suspended);
    }

    [Fact]
    public async Task UpdateAsync_PersistsAddedChildren()
    {
        Party c = NewContact();
        await ((IPartyWriter)_store).AddAsync(c, TestContext.Current.CancellationToken);

        c.AddAddress(Guid.NewGuid(), AddressKind.Billing, Address.Create("L1", "C", "1000", "BE"));
        c.AddEmail(Guid.NewGuid(), "new@acme.com");
        c.AddPhone(Guid.NewGuid(), PhoneKind.Mobile, "+1");
        c.AddExternalMapping(Guid.NewGuid(), "stripe", "cus_added");
        await ((IPartyWriter)_store).UpdateAsync(c, TestContext.Current.CancellationToken);

        Party? loaded = await _store.GetByIdAsync(
            PartyId.Create(c.Id), TestContext.Current.CancellationToken);
        loaded.ShouldNotBeNull();
        loaded.Addresses.ShouldHaveSingleItem();
        loaded.Emails.ShouldHaveSingleItem();
        loaded.Phones.ShouldHaveSingleItem();
        loaded.FindExternalId("stripe").ShouldBe("cus_added");
    }

    [Fact]
    public async Task UpdateAsync_PersistsRoleAdditions()
    {
        Party c = NewContact();
        await ((IPartyWriter)_store).AddAsync(c, TestContext.Current.CancellationToken);

        c.AddRole(PartyRoles.Supplier);
        await ((IPartyWriter)_store).UpdateAsync(c, TestContext.Current.CancellationToken);

        Party? loaded = await _store.GetByIdAsync(
            PartyId.Create(c.Id), TestContext.Current.CancellationToken);
        loaded.ShouldNotBeNull();
        loaded.HasRole(PartyRoles.Customer).ShouldBeTrue();
        loaded.HasRole(PartyRoles.Supplier).ShouldBeTrue();
    }

    /// <summary>
    /// Test factory that omits <c>ICurrentTenant</c> for the DbContext to bypass the
    /// multi-tenant query filter — sidesteps the AsyncLocal flakiness of <c>DataFilter</c>
    /// under parallel test execution.
    /// </summary>
    private sealed class TestFactory(DbContextOptions<PartiesDbContext> options)
        : IDbContextFactory<PartiesDbContext>
    {
        public ICurrentTenant Tenant { get; } = Substitute.For<ICurrentTenant>();

        public PartiesDbContext CreateDbContext() => new(options);

        public Task<PartiesDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new PartiesDbContext(options));
    }
}

[CollectionDefinition(ContactsDbSerialGroup.Name, DisableParallelization = true)]
public sealed class ContactsDbSerialGroup
{
    public const string Name = "Parties-Db-serial";
}
