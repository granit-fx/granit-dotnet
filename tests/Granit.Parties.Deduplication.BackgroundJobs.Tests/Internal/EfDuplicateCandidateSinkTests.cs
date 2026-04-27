using System.Diagnostics.Metrics;
using System.Text.Json;
using Granit.DataFiltering;
using Granit.Guids;
using Granit.MultiTenancy;
using Granit.Parties.Deduplication.BackgroundJobs.Diagnostics;
using Granit.Parties.Deduplication.BackgroundJobs.Internal;
using Granit.Parties.Deduplication.Domain;
using Granit.Parties.Domain.ValueObjects;
using Granit.Parties.EntityFrameworkCore.Deduplication;
using Granit.Parties.EntityFrameworkCore.Internal;
using Granit.Timing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Parties.Deduplication.BackgroundJobs.Tests.Internal;

public sealed class EfDuplicateCandidateSinkTests : IAsyncDisposable
{
    private readonly DataFilter _filter = new();
    private readonly StubCurrentTenant _tenant = new();
    private readonly StubDbContextFactory _factory;
    private readonly EfDuplicateCandidateSink _sink;
    private readonly DateTimeOffset _now = new(2026, 4, 27, 12, 0, 0, TimeSpan.Zero);

    public EfDuplicateCandidateSinkTests()
    {
        _factory = new StubDbContextFactory($"sink-{Guid.NewGuid()}", _tenant, _filter);

        IGuidGenerator guidGenerator = Substitute.For<IGuidGenerator>();
        guidGenerator.Create().Returns(_ => Guid.NewGuid());

        IClock clock = Substitute.For<IClock>();
        clock.Now.Returns(_now);

        var metrics = new PartiesDeduplicationMetrics(new StubMeterFactory());

        _sink = new EfDuplicateCandidateSink(_factory, guidGenerator, clock, metrics);
    }

    public async ValueTask DisposeAsync()
    {
        await using PartiesDbContext db = await _factory.CreateDbContextAsync();
        await db.Database.EnsureDeletedAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task UpsertAsync_orders_pair_and_inserts_new_row()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        // sourceId > candidateId — sink must reorder so PartyId < CandidateId on persist.
        Guid sourceId = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        Guid candidateId = new("11111111-1111-1111-1111-111111111111");

        DuplicateCandidate c = new(
            CandidateId: PartyId.Create(candidateId),
            Score: 0.92m,
            Tier: DuplicateMatchTier.Fuzzy,
            Signals: [new MatchSignal("NameTokenSet", 0.5m)]);

        int upserted = await _sink.UpsertAsync([c], sourceId, tenantId: null, ct);

        upserted.ShouldBe(1);

        await using PartiesDbContext readBack = await _factory.CreateDbContextAsync(ct);
        PartyDuplicateCandidate row = await readBack.DuplicateCandidates.SingleAsync(ct);
        row.PartyId.ShouldBe(candidateId);                  // lower id
        row.CandidateId.ShouldBe(sourceId);                  // higher id
        row.Tier.ShouldBe((int)DuplicateMatchTier.Fuzzy);
        row.Score.ShouldBe(0.92m);
        row.CreatedAt.ShouldBe(_now);
        row.UpdatedAt.ShouldBeNull();
    }

    [Fact]
    public async Task UpsertAsync_refreshes_existing_pair_instead_of_inserting()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Guid lower = new("11111111-1111-1111-1111-111111111111");
        Guid higher = new("22222222-2222-2222-2222-222222222222");

        // Seed one existing row.
        await using (PartiesDbContext db = await _factory.CreateDbContextAsync(ct))
        {
            db.DuplicateCandidates.Add(PartyDuplicateCandidate.Create(
                id: Guid.NewGuid(), tenantId: null, partyId: lower, candidateId: higher,
                tier: (int)DuplicateMatchTier.Blocking, score: 0.7m,
                signalsJson: JsonSerializer.Serialize(new[] { new MatchSignal("Old", 0.7m) }),
                createdAt: _now.AddDays(-1)));
            await db.SaveChangesAsync(ct);
        }

        // Detect the same pair with a higher Tier-3 score.
        DuplicateCandidate c = new(
            CandidateId: PartyId.Create(higher),
            Score: 0.95m,
            Tier: DuplicateMatchTier.Blocking,
            Signals: [new MatchSignal("NameTokenSet", 0.95m)]);

        int upserted = await _sink.UpsertAsync([c], lower, tenantId: null, ct);

        upserted.ShouldBe(1);

        await using PartiesDbContext readBack = await _factory.CreateDbContextAsync(ct);
        readBack.DuplicateCandidates.Count().ShouldBe(1);
        PartyDuplicateCandidate row = await readBack.DuplicateCandidates.SingleAsync(ct);
        row.Score.ShouldBe(0.95m);
        row.UpdatedAt.ShouldBe(_now);                     // refreshed timestamp set
    }

    [Fact]
    public async Task UpsertAsync_skips_dismissed_pair()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Guid lower = new("11111111-1111-1111-1111-111111111111");
        Guid higher = new("22222222-2222-2222-2222-222222222222");

        // Seed a dismissed row.
        await using (PartiesDbContext db = await _factory.CreateDbContextAsync(ct))
        {
            var dismissed = PartyDuplicateCandidate.Create(
                id: Guid.NewGuid(), tenantId: null, partyId: lower, candidateId: higher,
                tier: (int)DuplicateMatchTier.Blocking, score: 0.7m,
                signalsJson: "[]", createdAt: _now.AddDays(-1));
            dismissed.Dismiss(_now.AddHours(-1));
            db.DuplicateCandidates.Add(dismissed);
            await db.SaveChangesAsync(ct);
        }

        DuplicateCandidate c = new(
            CandidateId: PartyId.Create(higher), Score: 0.9m,
            Tier: DuplicateMatchTier.Blocking, Signals: []);

        int upserted = await _sink.UpsertAsync([c], lower, tenantId: null, ct);

        // Sink skipped the dismissed pair → 0 upserts.
        upserted.ShouldBe(0);

        await using PartiesDbContext readBack = await _factory.CreateDbContextAsync(ct);
        PartyDuplicateCandidate row = await readBack.DuplicateCandidates.SingleAsync(ct);
        row.DismissedAt.ShouldNotBeNull();
        row.Score.ShouldBe(0.7m);                          // not refreshed
    }

    [Fact]
    public async Task UpsertAsync_filters_self_match()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var id = Guid.NewGuid();

        DuplicateCandidate c = new(
            CandidateId: PartyId.Create(id), Score: 1.0m,
            Tier: DuplicateMatchTier.Deterministic, Signals: []);

        int upserted = await _sink.UpsertAsync([c], id, tenantId: null, ct);

        upserted.ShouldBe(0);

        await using PartiesDbContext readBack = await _factory.CreateDbContextAsync(ct);
        readBack.DuplicateCandidates.Count().ShouldBe(0);
    }
}

internal sealed class StubCurrentTenant : ICurrentTenant
{
    public bool IsAvailable => Id is not null;
    public Guid? Id { get; private set; }
    public string? Name { get; private set; }
    public IDisposable Change(Guid? id, string? name = null)
    {
        Guid? prev = Id;
        Id = id;
        return new RestoreScope(() => Id = prev);
    }
    private sealed class RestoreScope(Action a) : IDisposable { public void Dispose() => a(); }
}

internal sealed class StubDbContextFactory : IDbContextFactory<PartiesDbContext>
{
    private readonly DbContextOptions<PartiesDbContext> _options;
    private readonly StubCurrentTenant _tenant;
    private readonly DataFilter _filter;

    public StubDbContextFactory(string name, StubCurrentTenant tenant, DataFilter filter)
    {
        _tenant = tenant;
        _filter = filter;
        InMemoryDatabaseRoot root = new();
        _options = new DbContextOptionsBuilder<PartiesDbContext>()
            .UseInMemoryDatabase(name, root)
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .EnableServiceProviderCaching(false)
            .Options;
    }

    public PartiesDbContext CreateDbContext() => new(_options, _tenant, _filter);
    public Task<PartiesDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new PartiesDbContext(_options, _tenant, _filter));
}

internal sealed class StubMeterFactory : IMeterFactory
{
    public Meter Create(MeterOptions options) => new(options);
    public void Dispose() { }
}
