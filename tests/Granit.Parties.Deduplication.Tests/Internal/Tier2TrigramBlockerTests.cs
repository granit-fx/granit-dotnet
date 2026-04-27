using Granit.DataFiltering;
using Granit.Parties.Deduplication.Domain;
using Granit.Parties.Deduplication.Internal;
using Granit.Parties.Domain;
using Granit.Parties.EntityFrameworkCore.Deduplication;
using Granit.Parties.EntityFrameworkCore.Internal;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Parties.Deduplication.Tests.Internal;

/// <summary>
/// pg_trgm requires a real PostgreSQL connection to test functionally, so we only verify
/// the no-op fallback contract here. End-to-end Tier-2 behaviour will be covered by the
/// integration test suite (Testcontainers PostgreSQL) that lands with #1300.
/// </summary>
public sealed class Tier2TrigramBlockerTests : IAsyncDisposable
{
    private readonly DataFilter _filter = new();
    private readonly StubCurrentTenant _tenant = new();
    private readonly InMemoryPartiesDbContextFactory _factory;
    private readonly Tier2TrigramBlocker _blocker;

    public Tier2TrigramBlockerTests()
    {
        _factory = new InMemoryPartiesDbContextFactory($"tier2-{Guid.NewGuid()}", _tenant, _filter);
        IOptions<PartyDeduplicationOptions> options = Options.Create(new PartyDeduplicationOptions());
        _blocker = new Tier2TrigramBlocker(_factory, options);
    }

    public async ValueTask DisposeAsync()
    {
        await using PartiesDbContext db = await _factory.CreateDbContextAsync();
        await db.Database.EnsureDeletedAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task MatchAsync_is_noop_on_non_postgres_provider()
    {
        // InMemory provider is not Npgsql — Tier-2 must short-circuit and return [].
        CancellationToken ct = TestContext.Current.CancellationToken;
        PartyDraft draft = new(TenantId: null, Kind: PartyKind.Individual, Name: "Alice");

        IReadOnlyList<DuplicateCandidate> hits = await _blocker.MatchAsync(draft, ct);

        hits.ShouldBeEmpty();
    }

    [Fact]
    public async Task MatchAsync_returns_empty_for_blank_name()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        PartyDraft draft = new(TenantId: null, Kind: PartyKind.Individual, Name: "   ");

        IReadOnlyList<DuplicateCandidate> hits = await _blocker.MatchAsync(draft, ct);

        hits.ShouldBeEmpty();
    }
}
