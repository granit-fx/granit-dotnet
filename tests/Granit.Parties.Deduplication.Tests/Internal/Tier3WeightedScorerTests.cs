using Granit.DataFiltering;
using Granit.Parties.Deduplication.Domain;
using Granit.Parties.Deduplication.Internal;
using Granit.Parties.Domain;
using Granit.Parties.Domain.ValueObjects;
using Granit.Parties.EntityFrameworkCore.Internal;
using Shouldly;
using Xunit;

namespace Granit.Parties.Deduplication.Tests.Internal;

public sealed class Tier3WeightedScorerTests : IAsyncDisposable
{
    private readonly DataFilter _filter = new();
    private readonly StubCurrentTenant _tenant = new();
    private readonly InMemoryPartiesDbContextFactory _factory;
    private readonly Tier3WeightedScorer _scorer;

    public Tier3WeightedScorerTests()
    {
        _factory = new InMemoryPartiesDbContextFactory($"tier3-{Guid.NewGuid()}", _tenant, _filter);
        _scorer = new Tier3WeightedScorer(_factory);
    }

    public async ValueTask DisposeAsync()
    {
        await using PartiesDbContext db = await _factory.CreateDbContextAsync();
        await db.Database.EnsureDeletedAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ScoreAsync_empty_input_returns_empty()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        PartyDraft draft = new(TenantId: null, Kind: PartyKind.Individual, Name: "Alice");

        IReadOnlyList<DuplicateCandidate> ranked = await _scorer.ScoreAsync(draft, [], ct);

        ranked.ShouldBeEmpty();
    }

    [Fact]
    public async Task ScoreAsync_drops_candidates_below_min_score()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var other = Party.Create(
            id: Guid.NewGuid(), tenantId: null, kind: PartyKind.Individual,
            name: "Completely Different Name Here", defaultCurrency: "EUR");

        await using (PartiesDbContext db = await _factory.CreateDbContextAsync(ct))
        {
            db.Parties.Add(other);
            await db.SaveChangesAsync(ct);
        }

        PartyDraft draft = new(TenantId: null, Kind: PartyKind.Individual, Name: "Alice");
        DuplicateCandidate seed = new(
            CandidateId: PartyId.Create(other.Id),
            Score: 0.7m,
            Tier: DuplicateMatchTier.Blocking,
            Signals: [new MatchSignal("NameTrigram", 0.7m)]);

        IReadOnlyList<DuplicateCandidate> ranked = await _scorer.ScoreAsync(draft, [seed], ct);

        // Names too distant — Tier-3 should drop the candidate.
        ranked.ShouldBeEmpty();
    }

    [Fact]
    public async Task ScoreAsync_keeps_candidate_with_close_name()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var twin = Party.Create(
            id: Guid.NewGuid(), tenantId: null, kind: PartyKind.Individual,
            name: "Alice Dupont", defaultCurrency: "EUR");

        await using (PartiesDbContext db = await _factory.CreateDbContextAsync(ct))
        {
            db.Parties.Add(twin);
            await db.SaveChangesAsync(ct);
        }

        PartyDraft draft = new(TenantId: null, Kind: PartyKind.Individual, Name: "Alice Dupond");
        DuplicateCandidate seed = new(
            CandidateId: PartyId.Create(twin.Id),
            Score: 0.85m,
            Tier: DuplicateMatchTier.Blocking,
            Signals: [new MatchSignal("NameTrigram", 0.85m)]);

        IReadOnlyList<DuplicateCandidate> ranked = await _scorer.ScoreAsync(draft, [seed], ct);

        ranked.Count.ShouldBe(1);
        ranked[0].Tier.ShouldBe(DuplicateMatchTier.Fuzzy);
        ranked[0].Score.ShouldBeGreaterThan(DuplicateScoringWeights.MinScore);
        ranked[0].Signals.ShouldContain(s => s.Kind == "NameTokenSet");
    }

    [Fact]
    public async Task ScoreAsync_phone_partial_matches_on_last_seven_digits()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var twin = Party.Create(
            id: Guid.NewGuid(), tenantId: null, kind: PartyKind.Individual,
            name: "Alice Dupond", defaultCurrency: "EUR");
        twin.AddPhone(Guid.NewGuid(), PhoneKind.Mobile, "+32470123456");

        await using (PartiesDbContext db = await _factory.CreateDbContextAsync(ct))
        {
            db.Parties.Add(twin);
            await db.SaveChangesAsync(ct);
        }

        // Different country code, same subscriber suffix → last-7 digits match.
        PartyDraft draft = new(
            TenantId: null, Kind: PartyKind.Individual, Name: "Alice Dupond",
            Phones: ["+33470123456"]);

        DuplicateCandidate seed = new(
            CandidateId: PartyId.Create(twin.Id),
            Score: 0.9m,
            Tier: DuplicateMatchTier.Blocking,
            Signals: [new MatchSignal("NameTrigram", 0.9m)]);

        IReadOnlyList<DuplicateCandidate> ranked = await _scorer.ScoreAsync(draft, [seed], ct);

        ranked.Count.ShouldBe(1);
        ranked[0].Signals.ShouldContain(s => s.Kind == "PhonePartial");
    }
}

public sealed class DuplicateScoringWeightsTests
{
    [Fact]
    public void Weights_sum_to_one()
    {
        decimal total = DuplicateScoringWeights.Name
            + DuplicateScoringWeights.LastName
            + DuplicateScoringWeights.Address
            + DuplicateScoringWeights.EmailPartial
            + DuplicateScoringWeights.PhonePartial;

        total.ShouldBe(1.0m);
    }
}
