using Granit.DataFiltering;
using Granit.Domain;
using Granit.Parties.Domain;
using Granit.Parties.Domain.ValueObjects;
using Granit.Parties.EntityFrameworkCore;
using Granit.Parties.EntityFrameworkCore.Internal;
using Granit.Parties.Mergeable.Internal;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Parties.Mergeable.Tests.Integration;

/// <summary>
/// Postgres integration tests for <see cref="PartyParentReferenceRewriter"/>. The
/// <c>UPDATE parties SET ParentContactId = survivorId WHERE ParentContactId = loserId</c>
/// path requires a relational provider, hence Postgres rather than the in-memory provider.
/// </summary>
public sealed class PartyParentReferenceRewriterPostgresTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private readonly IDataFilter _dataFilter = Substitute.For<IDataFilter>();
    private TestPartiesDbContextFactory _factory = null!;
    private PartyParentReferenceRewriter _sut = null!;

    public PartyParentReferenceRewriterPostgresTests(PostgresFixture postgres)
    {
        _postgres = postgres;
        _dataFilter.Disable<IHasMergeTombstone>().Returns(Substitute.For<IDisposable>());
    }

    public async ValueTask InitializeAsync()
    {
        _factory = new TestPartiesDbContextFactory(_postgres.ConnectionString);
        await using PartiesDbContext db = await _factory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        string p = GranitPartiesDbProperties.DbTablePrefix;
        await db.Database.ExecuteSqlRawAsync(
            $"TRUNCATE TABLE {p}external_mappings, {p}addresses, {p}emails, {p}phones, {p}parties RESTART IDENTITY CASCADE;",
            TestContext.Current.CancellationToken);

        _sut = new PartyParentReferenceRewriter(_factory, _dataFilter);
    }

    public ValueTask DisposeAsync() => _factory?.DisposeAsync() ?? ValueTask.CompletedTask;

    [Fact]
    public async Task RewriteAsync_ReparentsLoserChildrenOntoSurvivor()
    {
        Party survivor = NewParty("Holding S");
        Party loser = NewParty("Holding L");
        Party child1 = NewParty("Sub 1");
        child1.AttachToParent(PartyId.Create(loser.Id), parentTenantId: null);
        Party child2 = NewParty("Sub 2");
        child2.AttachToParent(PartyId.Create(loser.Id), parentTenantId: null);
        Party unrelated = NewParty("Other Sub");
        unrelated.AttachToParent(PartyId.Create(survivor.Id), parentTenantId: null);

        await SeedAsync(survivor, loser, child1, child2, unrelated);

        int rewritten = await _sut.RewriteAsync(survivor.Id, loser.Id, TestContext.Current.CancellationToken);
        rewritten.ShouldBe(2);

        await using PartiesDbContext db = await _factory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        var survivorPartyId = PartyId.Create(survivor.Id);
        int childrenOfSurvivor = await db.Parties
            .CountAsync(p => p.ParentContactId == survivorPartyId, TestContext.Current.CancellationToken);
        childrenOfSurvivor.ShouldBe(3);

        var loserPartyId = PartyId.Create(loser.Id);
        int childrenOfLoser = await db.Parties
            .CountAsync(p => p.ParentContactId == loserPartyId, TestContext.Current.CancellationToken);
        childrenOfLoser.ShouldBe(0);
    }

    [Fact]
    public async Task RewriteAsync_ReturnsZero_WhenLoserHasNoChildren()
    {
        Party survivor = NewParty("S");
        Party loser = NewParty("L");
        await SeedAsync(survivor, loser);

        int rewritten = await _sut.RewriteAsync(survivor.Id, loser.Id, TestContext.Current.CancellationToken);
        rewritten.ShouldBe(0);
    }

    [Fact]
    public async Task CountAsync_ReturnsLoserChildCount_WithoutMutating()
    {
        Party survivor = NewParty("S");
        Party loser = NewParty("L");
        Party c1 = NewParty("C1"); c1.AttachToParent(PartyId.Create(loser.Id), parentTenantId: null);
        Party c2 = NewParty("C2"); c2.AttachToParent(PartyId.Create(loser.Id), parentTenantId: null);
        await SeedAsync(survivor, loser, c1, c2);

        int count = await _sut.CountAsync(survivor.Id, loser.Id, TestContext.Current.CancellationToken);
        count.ShouldBe(2);

        // Confirm CountAsync did not mutate.
        await using PartiesDbContext db = await _factory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        var loserPartyId = PartyId.Create(loser.Id);
        int still = await db.Parties
            .CountAsync(p => p.ParentContactId == loserPartyId, TestContext.Current.CancellationToken);
        still.ShouldBe(2);
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
