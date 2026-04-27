using Granit.DataFiltering;
using Granit.Parties.Deduplication.Domain;
using Granit.Parties.Deduplication.Internal;
using Granit.Parties.Domain;
using Granit.Parties.EntityFrameworkCore.Deduplication;
using Granit.Parties.EntityFrameworkCore.Internal;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Parties.Deduplication.Tests.Internal;

/// <summary>
/// End-to-end composer test against the InMemory provider. Tier-2 is a no-op on InMemory
/// so the composer effectively exercises Tier-1 + (Tier-3 over the empty Tier-2 set).
/// Real Tier-2 behaviour is covered by the Testcontainers-PostgreSQL integration suite
/// landing with story #1300.
/// </summary>
public sealed class DefaultPartyDuplicateDetectorTests : IAsyncDisposable
{
    private readonly DataFilter _filter = new();
    private readonly StubCurrentTenant _tenant = new();
    private readonly InMemoryPartiesDbContextFactory _factory;
    private readonly DefaultPartyDuplicateDetector _detector;

    public DefaultPartyDuplicateDetectorTests()
    {
        _factory = new InMemoryPartiesDbContextFactory($"composer-{Guid.NewGuid()}", _tenant, _filter);
        IOptions<PartyDeduplicationOptions> options = Options.Create(new PartyDeduplicationOptions());

        Tier1DeterministicMatcher tier1 = new(_factory);
        Tier2TrigramBlocker tier2 = new(_factory, options);
        Tier3WeightedScorer tier3 = new(_factory);

        _detector = new DefaultPartyDuplicateDetector(
            _factory, tier1, tier2, tier3, _filter,
            NullLogger<DefaultPartyDuplicateDetector>.Instance);
    }

    public async ValueTask DisposeAsync()
    {
        await using PartiesDbContext db = await _factory.CreateDbContextAsync();
        await db.Database.EnsureDeletedAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task FindCandidatesAsync_surfaces_Tier1_email_match_with_score_one()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var seeded = Party.Create(
            id: Guid.NewGuid(), tenantId: null, kind: PartyKind.Individual,
            name: "Alice", defaultCurrency: "EUR");
        seeded.AddEmail(Guid.NewGuid(), "alice@gmail.com");

        await using (PartiesDbContext db = await _factory.CreateDbContextAsync(ct))
        {
            db.Parties.Add(seeded);
            await db.SaveChangesAsync(ct);
        }

        PartyDraft draft = new(
            TenantId: null, Kind: PartyKind.Individual, Name: "Alice Twin",
            Emails: ["a.l.i.c.e+x@gmail.com"]);

        IReadOnlyList<DuplicateCandidate> hits = await _detector.FindCandidatesAsync(draft, ct);

        hits.Count.ShouldBe(1);
        hits[0].Score.ShouldBe(1.0m);
        hits[0].Tier.ShouldBe(DuplicateMatchTier.Deterministic);
    }

    [Fact]
    public async Task FindCandidatesAsync_returns_empty_when_no_party_matches()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        PartyDraft draft = new(
            TenantId: null, Kind: PartyKind.Individual, Name: "Nobody",
            Emails: ["nobody@example.com"]);

        IReadOnlyList<DuplicateCandidate> hits = await _detector.FindCandidatesAsync(draft, ct);

        hits.ShouldBeEmpty();
    }

    [Fact]
    public async Task ScanTenantAsync_counts_one_pair_for_two_parties_sharing_email()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var tenantId = Guid.NewGuid();

        var first = Party.Create(
            id: Guid.NewGuid(), tenantId: tenantId, kind: PartyKind.Individual,
            name: "Alice", defaultCurrency: "EUR");
        first.AddEmail(Guid.NewGuid(), "shared@example.com");

        var second = Party.Create(
            id: Guid.NewGuid(), tenantId: tenantId, kind: PartyKind.Individual,
            name: "Bob", defaultCurrency: "EUR");
        second.AddEmail(Guid.NewGuid(), "shared@example.com");

        // Bystander: different tenant, same email — must NOT be counted.
        var bystander = Party.Create(
            id: Guid.NewGuid(), tenantId: Guid.NewGuid(), kind: PartyKind.Individual,
            name: "Stranger", defaultCurrency: "EUR");
        bystander.AddEmail(Guid.NewGuid(), "shared@example.com");

        await using (PartiesDbContext db = await _factory.CreateDbContextAsync(ct))
        {
            db.Parties.AddRange(first, second, bystander);
            await db.SaveChangesAsync(ct);
        }

        int pairCount = await _detector.ScanTenantAsync(tenantId, ct);

        // Two parties in the tenant share an email → 1 unique pair.
        pairCount.ShouldBe(1);
    }
}
