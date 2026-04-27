using Granit.DataFiltering;
using Granit.Domain;
using Granit.Mergeable.Exceptions;
using Granit.Parties.Domain;
using Granit.Parties.EntityFrameworkCore;
using Granit.Parties.EntityFrameworkCore.Internal;
using Granit.Parties.Mergeable.Internal;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Parties.Mergeable.Tests.Integration;

/// <summary>
/// Postgres integration tests for <see cref="PartyChildrenReferenceRewriter"/>. Exercises
/// the bulk SQL paths (<c>ExecuteUpdateAsync</c> + <c>ExecuteDeleteAsync</c>) that the
/// in-memory provider cannot run.
/// </summary>
public sealed class PartyChildrenReferenceRewriterPostgresTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private readonly IDataFilter _dataFilter = Substitute.For<IDataFilter>();
    private TestPartiesDbContextFactory _factory = null!;
    private PartyChildrenReferenceRewriter _sut = null!;

    public PartyChildrenReferenceRewriterPostgresTests(PostgresFixture postgres)
    {
        _postgres = postgres;
        _dataFilter.Disable<IHasMergeTombstone>().Returns(Substitute.For<IDisposable>());
    }

    public async ValueTask InitializeAsync()
    {
        _factory = new TestPartiesDbContextFactory(_postgres.ConnectionString);
        await using PartiesDbContext db = await _factory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        // Tables created via EnsureCreated; truncate between tests for isolation.
        // Use the configured prefix (default "contacts_") so tests stay aligned with whatever
        // GranitPartiesDbProperties.DbTablePrefix is set to in the future.
        string p = GranitPartiesDbProperties.DbTablePrefix;
        await db.Database.ExecuteSqlRawAsync(
            $"TRUNCATE TABLE {p}external_mappings, {p}addresses, {p}emails, {p}phones, {p}parties RESTART IDENTITY CASCADE;",
            TestContext.Current.CancellationToken);

        _sut = new PartyChildrenReferenceRewriter(_factory, _dataFilter);
    }

    public ValueTask DisposeAsync() => _factory?.DisposeAsync() ?? ValueTask.CompletedTask;

    [Fact]
    public async Task EmailDuplicate_IsDeletedFromLoser_AndUniqueIsMoved()
    {
        Party survivor = NewParty("Acme S");
        survivor.AddEmail(Guid.NewGuid(), "shared@acme.com", isPrimary: true);
        survivor.AddEmail(Guid.NewGuid(), "ops@acme.com", isPrimary: false);

        Party loser = NewParty("Acme L");
        loser.AddEmail(Guid.NewGuid(), "shared@acme.com", isPrimary: false);
        loser.AddEmail(Guid.NewGuid(), "billing@acme.com", isPrimary: false);

        await SeedAsync(survivor, loser);

        await _sut.RewriteAsync(survivor.Id, loser.Id, TestContext.Current.CancellationToken);

        await using PartiesDbContext db = await _factory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        List<string> survivorEmails = await db.Set<PartyEmail>()
            .Where(e => EF.Property<Guid>(e, "PartyId") == survivor.Id)
            .Select(e => e.Address)
            .OrderBy(s => s)
            .ToListAsync(TestContext.Current.CancellationToken);
        survivorEmails.ShouldBe(["billing@acme.com", "ops@acme.com", "shared@acme.com"]);

        int loserEmailCount = await db.Set<PartyEmail>()
            .CountAsync(e => EF.Property<Guid>(e, "PartyId") == loser.Id, TestContext.Current.CancellationToken);
        loserEmailCount.ShouldBe(0);
    }

    [Fact]
    public async Task PrimaryEmail_OnLoser_IsDemoted_WhenSurvivorAlreadyHasOne()
    {
        Party survivor = NewParty("Acme S");
        survivor.AddEmail(Guid.NewGuid(), "owner@acme.com", isPrimary: true);

        Party loser = NewParty("Acme L");
        loser.AddEmail(Guid.NewGuid(), "owner-old@acme.com", isPrimary: true);

        await SeedAsync(survivor, loser);

        await _sut.RewriteAsync(survivor.Id, loser.Id, TestContext.Current.CancellationToken);

        await using PartiesDbContext db = await _factory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        List<PartyEmail> emails = await db.Set<PartyEmail>()
            .Where(e => EF.Property<Guid>(e, "PartyId") == survivor.Id)
            .ToListAsync(TestContext.Current.CancellationToken);

        emails.Count.ShouldBe(2);
        emails.Count(e => e.IsPrimary).ShouldBe(1);
        emails.Single(e => e.IsPrimary).Address.ShouldBe("owner@acme.com");
    }

    [Fact]
    public async Task DefaultAddress_OnLoser_IsDemotedPerKind_WhenSurvivorHasOne()
    {
        Party survivor = NewParty("Acme S");
        survivor.AddAddress(
            Guid.NewGuid(),
            AddressKind.Billing,
            Address.Create("Rue 1", "Brussels", "1000", "BE"),
            isDefault: true);

        Party loser = NewParty("Acme L");
        loser.AddAddress(
            Guid.NewGuid(),
            AddressKind.Billing,
            Address.Create("Rue 99", "Brussels", "1000", "BE"),
            isDefault: true);
        loser.AddAddress(
            Guid.NewGuid(),
            AddressKind.Shipping,
            Address.Create("Av Louise", "Brussels", "1050", "BE"),
            isDefault: true);

        await SeedAsync(survivor, loser);

        await _sut.RewriteAsync(survivor.Id, loser.Id, TestContext.Current.CancellationToken);

        await using PartiesDbContext db = await _factory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        List<PartyAddress> addresses = await db.Set<PartyAddress>()
            .Where(a => EF.Property<Guid>(a, "PartyId") == survivor.Id)
            .ToListAsync(TestContext.Current.CancellationToken);

        addresses.Count.ShouldBe(3);
        addresses.Count(a => a.Kind == AddressKind.Billing && a.IsDefault).ShouldBe(1);
        addresses.Single(a => a.Kind == AddressKind.Billing && a.IsDefault).Value.Line1.ShouldBe("Rue 1");
        addresses.Single(a => a.Kind == AddressKind.Shipping).IsDefault.ShouldBeTrue();
    }

    [Fact]
    public async Task AddressStructuralDuplicate_IsDedupedByVoEquality()
    {
        var shared = Address.Create("Same St 1", "Brussels", "1000", "BE", state: "BXL");
        Party survivor = NewParty("Acme S");
        survivor.AddAddress(Guid.NewGuid(), AddressKind.Billing, shared);

        Party loser = NewParty("Acme L");
        loser.AddAddress(
            Guid.NewGuid(),
            AddressKind.Billing,
            Address.Create("Same St 1", "Brussels", "1000", "BE", state: "BXL"));
        loser.AddAddress(
            Guid.NewGuid(),
            AddressKind.Shipping,
            Address.Create("Other St 9", "Antwerp", "2000", "BE"));

        await SeedAsync(survivor, loser);

        await _sut.RewriteAsync(survivor.Id, loser.Id, TestContext.Current.CancellationToken);

        await using PartiesDbContext db = await _factory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        int survivorAddressCount = await db.Set<PartyAddress>()
            .CountAsync(a => EF.Property<Guid>(a, "PartyId") == survivor.Id, TestContext.Current.CancellationToken);
        survivorAddressCount.ShouldBe(2); // 1 original + 1 unique (shipping); the duplicate billing was deleted.
    }

    [Fact]
    public async Task ExternalMappingConflict_OnSameProvider_ThrowsMergeException()
    {
        Party survivor = NewParty("Acme S");
        survivor.AddExternalMapping(Guid.NewGuid(), "stripe", "cus_survivor");

        Party loser = NewParty("Acme L");
        loser.AddExternalMapping(Guid.NewGuid(), "stripe", "cus_loser");

        await SeedAsync(survivor, loser);

        MergeException ex = await Should.ThrowAsync<MergeException>(() =>
            _sut.RewriteAsync(survivor.Id, loser.Id, TestContext.Current.CancellationToken));
        ex.Message.ShouldContain("stripe");
    }

    [Fact]
    public async Task ExternalMappingDuplicate_SameProviderAndExternalId_IsDeleted()
    {
        Party survivor = NewParty("Acme S");
        survivor.AddExternalMapping(Guid.NewGuid(), "stripe", "cus_same");

        Party loser = NewParty("Acme L");
        loser.AddExternalMapping(Guid.NewGuid(), "stripe", "cus_same");
        loser.AddExternalMapping(Guid.NewGuid(), "mollie", "cst_unique");

        await SeedAsync(survivor, loser);

        await _sut.RewriteAsync(survivor.Id, loser.Id, TestContext.Current.CancellationToken);

        await using PartiesDbContext db = await _factory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        List<PartyExternalMapping> mappings = await db.Set<PartyExternalMapping>()
            .Where(m => EF.Property<Guid>(m, "PartyId") == survivor.Id)
            .OrderBy(m => m.ProviderName)
            .ToListAsync(TestContext.Current.CancellationToken);

        mappings.Select(m => m.ProviderName).ShouldBe(["mollie", "stripe"]);
    }

    [Fact]
    public async Task EmailCapExceeded_AfterMerge_ThrowsMergeException()
    {
        // Cap is 50; survivor + loser unique emails > 50 must throw.
        Party survivor = NewParty("Big S");
        for (int i = 0; i < 30; i++)
        {
            survivor.AddEmail(Guid.NewGuid(), $"s{i}@acme.com", isPrimary: i == 0);
        }
        Party loser = NewParty("Big L");
        for (int i = 0; i < 25; i++)
        {
            loser.AddEmail(Guid.NewGuid(), $"l{i}@acme.com", isPrimary: i == 0);
        }
        await SeedAsync(survivor, loser);

        MergeException ex = await Should.ThrowAsync<MergeException>(() =>
            _sut.RewriteAsync(survivor.Id, loser.Id, TestContext.Current.CancellationToken));
        ex.Message.ShouldContain("Email cap exceeded");
    }

    [Fact]
    public async Task CountAsync_ReturnsTotalLoserChildRowCount()
    {
        Party survivor = NewParty("Acme S");
        survivor.AddEmail(Guid.NewGuid(), "s@acme.com", isPrimary: true);

        Party loser = NewParty("Acme L");
        loser.AddEmail(Guid.NewGuid(), "l1@acme.com");
        loser.AddEmail(Guid.NewGuid(), "l2@acme.com");
        loser.AddPhone(Guid.NewGuid(), PhoneKind.Mobile, "+3221111111");
        loser.AddAddress(Guid.NewGuid(), AddressKind.Billing,
            Address.Create("Rue 1", "Brussels", "1000", "BE"));

        await SeedAsync(survivor, loser);

        int count = await _sut.CountAsync(survivor.Id, loser.Id, TestContext.Current.CancellationToken);
        count.ShouldBe(4);
    }

    private async Task SeedAsync(params Party[] parties)
    {
        await using PartiesDbContext db = await _factory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        db.Parties.AddRange(parties);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static Party NewParty(string name) =>
        Party.Create(Guid.NewGuid(), tenantId: null, PartyKind.Company, name, "EUR");
}
