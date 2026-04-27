using Granit.DataFiltering;
using Granit.Domain;
using Granit.Mergeable;
using Granit.Parties.Domain;
using Granit.Parties.Domain.ValueObjects;
using Granit.Parties.EntityFrameworkCore.Internal;
using Granit.Parties.Mergeable.Internal;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Parties.Mergeable.Tests.Internal;

/// <summary>
/// In-memory tests for PartyMergeableAggregateAdapter. ExecuteUpdateAsync requires a relational
/// provider, so the chain-collapse path is exercised in integration tests against PostgreSQL
/// rather than here. The unit tests validate the load + tombstone application paths plus the
/// data-filter bypass contract.
/// </summary>
public sealed class PartyMergeableAggregateAdapterTests
{
    private readonly IDbContextFactory<PartiesDbContext> _factory = new InMemoryFactory();
    private readonly IDataFilter _dataFilter = Substitute.For<IDataFilter>();
    private readonly PartyMergeableAggregateAdapter _adapter;

    public PartyMergeableAggregateAdapterTests()
    {
        _dataFilter.Disable<IHasMergeTombstone>().Returns(Substitute.For<IDisposable>());
        _adapter = new PartyMergeableAggregateAdapter(_factory, _dataFilter);
    }

    [Fact]
    public async Task LoadAsync_DisablesTombstoneFilter_ForTheLookup()
    {
        IDisposable disposable = Substitute.For<IDisposable>();
        _dataFilter.Disable<IHasMergeTombstone>().Returns(disposable);

        await _adapter.LoadAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        _dataFilter.Received(1).Disable<IHasMergeTombstone>();
        disposable.Received(1).Dispose();
    }

    [Fact]
    public async Task LoadAsync_ReturnsNull_WhenPartyDoesNotExist()
    {
        Party? loaded = await _adapter.LoadAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        loaded.ShouldBeNull();
    }

    [Fact]
    public async Task LoadAsync_ReturnsTheStoredParty()
    {
        Party party = NewParty("Acme");
        await SeedAsync(party);

        Party? loaded = await _adapter.LoadAsync(party.Id, TestContext.Current.CancellationToken);

        loaded.ShouldNotBeNull();
        loaded!.Name.ShouldBe("Acme");
    }

    [Fact]
    public void ApplyTombstone_SetsMergedIntoIdAndMergedAt()
    {
        Party loser = NewParty("Loser");
        var survivorId = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        _adapter.ApplyTombstone(loser, survivorId, now);

        loser.MergedIntoId.ShouldBe(survivorId);
        loser.MergedAt.ShouldBe(now);
    }

    [Fact]
    public void ApplyTombstone_RejectsNullLoser() =>
        Should.Throw<ArgumentNullException>(() =>
            _adapter.ApplyTombstone(null!, Guid.NewGuid(), DateTimeOffset.UtcNow));

    private async Task SeedAsync(Party party)
    {
        await using PartiesDbContext db = await _factory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        db.Parties.Add(party);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static Party NewParty(string name) =>
        Party.Create(Guid.NewGuid(), tenantId: null, PartyKind.Company, name, "EUR");

    private sealed class InMemoryFactory : IDbContextFactory<PartiesDbContext>
    {
        private readonly DbContextOptions<PartiesDbContext> _options =
            new DbContextOptionsBuilder<PartiesDbContext>()
                .UseInMemoryDatabase($"parties-mergeable-{Guid.NewGuid()}")
                .ConfigureWarnings(w =>
                    w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;

        public PartiesDbContext CreateDbContext() => new(_options);

        public Task<PartiesDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new PartiesDbContext(_options));
    }
}
