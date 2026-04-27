using Granit.DataFiltering;
using Granit.Parties.Deduplication.Domain;
using Granit.Parties.Deduplication.Internal;
using Granit.Parties.Domain;
using Granit.Parties.EntityFrameworkCore.Internal;
using Shouldly;
using Xunit;

namespace Granit.Parties.Deduplication.Tests.Internal;

public sealed class Tier1DeterministicMatcherTests : IAsyncDisposable
{
    private readonly DataFilter _filter = new();
    private readonly StubCurrentTenant _tenant = new();
    private readonly InMemoryPartiesDbContextFactory _factory;
    private readonly Tier1DeterministicMatcher _matcher;

    public Tier1DeterministicMatcherTests()
    {
        _factory = new InMemoryPartiesDbContextFactory($"tier1-{Guid.NewGuid()}", _tenant, _filter);
        _matcher = new Tier1DeterministicMatcher(_factory);
    }

    public async ValueTask DisposeAsync()
    {
        await using PartiesDbContext db = await _factory.CreateDbContextAsync();
        await db.Database.EnsureDeletedAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task MatchAsync_returns_empty_when_draft_has_no_canonical_inputs()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        PartyDraft draft = new(TenantId: null, Kind: PartyKind.Individual, Name: "Alice");

        IReadOnlyList<DuplicateCandidate> hits = await _matcher.MatchAsync(draft, ct);

        hits.ShouldBeEmpty();
    }

    [Fact]
    public async Task MatchAsync_finds_email_match_after_canonicalisation()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;

        // Seed: Alice with verbatim email "Alice+Newsletter@Gmail.com"; canonical projection
        // is "alice@gmail.com" via the interceptor.
        var alice = Party.Create(
            id: Guid.NewGuid(), tenantId: null, kind: PartyKind.Individual,
            name: "Alice", defaultCurrency: "EUR");
        alice.AddEmail(Guid.NewGuid(), "Alice+Newsletter@Gmail.com");

        await using (PartiesDbContext db = await _factory.CreateDbContextAsync(ct))
        {
            db.Parties.Add(alice);
            await db.SaveChangesAsync(ct);
        }

        // Draft uses a DIFFERENT verbatim form that canonicalises to the same key.
        PartyDraft draft = new(
            TenantId: null, Kind: PartyKind.Individual, Name: "Alice Doe",
            Emails: ["a.l.i.c.e@gmail.com"]);

        IReadOnlyList<DuplicateCandidate> hits = await _matcher.MatchAsync(draft, ct);

        hits.Count.ShouldBe(1);
        hits[0].CandidateId.Value.ShouldBe(alice.Id);
        hits[0].Score.ShouldBe(1.0m);
        hits[0].Tier.ShouldBe(DuplicateMatchTier.Deterministic);
        hits[0].Signals.ShouldContain(s => s.Kind == "EmailExact");
    }

    [Fact]
    public async Task MatchAsync_finds_taxid_match_using_canonical_form()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;

        var acme = Party.Create(
            id: Guid.NewGuid(), tenantId: null, kind: PartyKind.Company,
            name: "Acme SA", defaultCurrency: "EUR",
            taxId: "BE 0123.456.789");

        await using (PartiesDbContext db = await _factory.CreateDbContextAsync(ct))
        {
            db.Parties.Add(acme);
            await db.SaveChangesAsync(ct);
        }

        PartyDraft draft = new(
            TenantId: null, Kind: PartyKind.Company, Name: "Acme International",
            TaxId: "be-0123-456-789");

        IReadOnlyList<DuplicateCandidate> hits = await _matcher.MatchAsync(draft, ct);

        hits.Count.ShouldBe(1);
        hits[0].CandidateId.Value.ShouldBe(acme.Id);
        hits[0].Signals.ShouldContain(s => s.Kind == "TaxIdExact");
    }

    [Fact]
    public async Task MatchAsync_collapses_multiple_field_hits_for_the_same_party()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;

        var bob = Party.Create(
            id: Guid.NewGuid(), tenantId: null, kind: PartyKind.Individual,
            name: "Bob", defaultCurrency: "EUR");
        bob.AddEmail(Guid.NewGuid(), "bob@example.com");
        bob.AddPhone(Guid.NewGuid(), PhoneKind.Mobile, "+32470123456");

        await using (PartiesDbContext db = await _factory.CreateDbContextAsync(ct))
        {
            db.Parties.Add(bob);
            await db.SaveChangesAsync(ct);
        }

        PartyDraft draft = new(
            TenantId: null, Kind: PartyKind.Individual, Name: "Robert",
            Emails: ["bob@example.com"], Phones: ["+32 470 12 34 56"]);

        IReadOnlyList<DuplicateCandidate> hits = await _matcher.MatchAsync(draft, ct);

        // One candidate, two signals.
        hits.Count.ShouldBe(1);
        hits[0].Signals.Count.ShouldBe(2);
        hits[0].Signals.ShouldContain(s => s.Kind == "EmailExact");
        hits[0].Signals.ShouldContain(s => s.Kind == "PhoneExact");
    }
}
