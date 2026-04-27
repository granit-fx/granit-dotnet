using Granit.Domain;
using Granit.Guids;
using Granit.Mergeable;
using Granit.Mergeable.Domain;
using Granit.Mergeable.EntityFrameworkCore;
using Granit.Mergeable.EntityFrameworkCore.Internal;
using Granit.Mergeable.Exceptions;
using Granit.Timing;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Mergeable.EntityFrameworkCore.Tests;

/// <summary>
/// Behaviour tests on a fake aggregate + adapter — exercises the orchestration pipeline
/// without needing the Party domain. The advisory-lock SQL is no-op on InMemory provider,
/// so tests focus on validation, dry-run, scatter-gather, idempotency, and tombstone.
/// </summary>
public sealed class EfMergeServiceTests
{
    private static readonly Guid SurvivorId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid LoserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTimeOffset Now = new(2026, 4, 27, 12, 0, 0, TimeSpan.Zero);

    private readonly IDbContextFactory<MergeableDbContext> _factory;
    private readonly IGuidGenerator _guidGenerator = Substitute.For<IGuidGenerator>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private int _idCounter;

    public EfMergeServiceTests()
    {
        _factory = new InMemoryFactory();
        _clock.Now.Returns(Now);
        // Sequential GUIDs so the idempotency PK is deterministic for assertion ordering.
        _guidGenerator.Create().Returns(_ => Guid.Parse($"33333333-3333-3333-3333-{++_idCounter:D12}"));
    }

    [Fact]
    public async Task MergePreviewAsync_ReturnsConflictsAndCounts_WithoutCommitting()
    {
        FakeAggregate survivor = new(SurvivorId, "Survivor");
        FakeAggregate loser = new(LoserId, "Loser");
        FakeAdapter adapter = new(survivor, loser);
        FakeRewriter rewriter = new(rewriteCount: 5, countCount: 5);

        var sut = new EfMergeService<FakeAggregate>(adapter, [rewriter], _factory, _guidGenerator, _clock);

        MergeResult<FakeAggregate> result = await sut.MergePreviewAsync(SurvivorId, LoserId, TestContext.Current.CancellationToken);

        result.DryRun.ShouldBeTrue();
        result.Merged.ShouldBeNull();
        result.Conflicts.ShouldHaveSingleItem();
        result.RewriteCounts["fake.RefId"].ShouldBe(5);
        rewriter.RewriteCalls.ShouldBe(0); // Dry-run should NOT call RewriteAsync
        rewriter.CountCalls.ShouldBe(1);
        adapter.PersistCalls.ShouldBe(0);
        adapter.TombstoneCalls.ShouldBe(0);
    }

    [Fact]
    public async Task MergeAsync_LiveMerge_AppliesScalarsRewritesAndTombstones()
    {
        FakeAggregate survivor = new(SurvivorId, "Survivor");
        FakeAggregate loser = new(LoserId, "Loser");
        FakeAdapter adapter = new(survivor, loser);
        FakeRewriter rewriter = new(rewriteCount: 17, countCount: 17);
        var sut = new EfMergeService<FakeAggregate>(adapter, [rewriter], _factory, _guidGenerator, _clock);

        var request = new MergeRequest(SurvivorId, LoserId, MergeFieldChoices.Empty, Reason: "manual");
        MergeResult<FakeAggregate> result = await sut.MergeAsync(request, TestContext.Current.CancellationToken);

        result.DryRun.ShouldBeFalse();
        result.Merged.ShouldBe(survivor);
        result.RewriteCounts["fake.RefId"].ShouldBe(17);
        survivor.MergeFromCalls.ShouldBe(1);
        rewriter.RewriteCalls.ShouldBe(1);
        adapter.TombstoneCalls.ShouldBe(1);
        adapter.AppliedTombstoneSurvivorId.ShouldBe(SurvivorId);
        adapter.AppliedTombstoneAt.ShouldBe(Now);
        adapter.PersistCalls.ShouldBe(1);
        adapter.ChainCollapseCalls.ShouldBe(1);
    }

    [Fact]
    public async Task MergeAsync_SurvivorAlreadyTombstoned_Throws()
    {
        FakeAggregate survivor = new(SurvivorId, "Survivor") { MergedIntoId = Guid.NewGuid() };
        FakeAggregate loser = new(LoserId, "Loser");
        FakeAdapter adapter = new(survivor, loser);
        var sut = new EfMergeService<FakeAggregate>(adapter, [], _factory, _guidGenerator, _clock);

        await Should.ThrowAsync<MergeException>(() =>
            sut.MergeAsync(new MergeRequest(SurvivorId, LoserId, MergeFieldChoices.Empty), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task MergeAsync_LoserAlreadyTombstoned_Throws()
    {
        FakeAggregate survivor = new(SurvivorId, "Survivor");
        FakeAggregate loser = new(LoserId, "Loser") { MergedIntoId = Guid.NewGuid() };
        FakeAdapter adapter = new(survivor, loser);
        var sut = new EfMergeService<FakeAggregate>(adapter, [], _factory, _guidGenerator, _clock);

        await Should.ThrowAsync<MergeException>(() =>
            sut.MergeAsync(new MergeRequest(SurvivorId, LoserId, MergeFieldChoices.Empty), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task MergeAsync_SameSurvivorAndLoser_Throws()
    {
        FakeAggregate one = new(SurvivorId, "X");
        FakeAdapter adapter = new(one, one);
        var sut = new EfMergeService<FakeAggregate>(adapter, [], _factory, _guidGenerator, _clock);

        await Should.ThrowAsync<MergeException>(() =>
            sut.MergeAsync(new MergeRequest(SurvivorId, SurvivorId, MergeFieldChoices.Empty), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task MergeAsync_LoserNotFound_Throws()
    {
        FakeAggregate survivor = new(SurvivorId, "Survivor");
        FakeAdapter adapter = new(survivor, loser: null);
        var sut = new EfMergeService<FakeAggregate>(adapter, [], _factory, _guidGenerator, _clock);

        await Should.ThrowAsync<MergeException>(() =>
            sut.MergeAsync(new MergeRequest(SurvivorId, LoserId, MergeFieldChoices.Empty), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task MergeAsync_IdempotencyReplay_ReturnsCachedResultWithoutReExecuting()
    {
        FakeAggregate survivor = new(SurvivorId, "Survivor");
        FakeAggregate loser = new(LoserId, "Loser");
        FakeAdapter adapter = new(survivor, loser);
        FakeRewriter rewriter = new(rewriteCount: 7, countCount: 7);
        var sut = new EfMergeService<FakeAggregate>(adapter, [rewriter], _factory, _guidGenerator, _clock);
        var request = new MergeRequest(SurvivorId, LoserId, MergeFieldChoices.Empty,
            Reason: "manual", IdempotencyKey: "the-key");

        MergeResult<FakeAggregate> first = await sut.MergeAsync(request, TestContext.Current.CancellationToken);
        first.RewriteCounts["fake.RefId"].ShouldBe(7);

        // Second call with same key + same body → cached replay, no second rewrite.
        MergeResult<FakeAggregate> replay = await sut.MergeAsync(request, TestContext.Current.CancellationToken);
        replay.DryRun.ShouldBeFalse();
        replay.Merged.ShouldBeNull("cached replays don't reload the aggregate");
        replay.RewriteCounts["fake.RefId"].ShouldBe(7);
        rewriter.RewriteCalls.ShouldBe(1, "second call must hit the cache, not re-rewrite");
    }

    [Fact]
    public async Task MergeAsync_IdempotencyKeyReusedWithDifferentBody_Throws()
    {
        FakeAggregate survivor = new(SurvivorId, "Survivor");
        FakeAggregate loser = new(LoserId, "Loser");
        FakeAdapter adapter = new(survivor, loser);
        var sut = new EfMergeService<FakeAggregate>(adapter, [], _factory, _guidGenerator, _clock);

        await sut.MergeAsync(
            new MergeRequest(SurvivorId, LoserId, MergeFieldChoices.Empty, Reason: "first", IdempotencyKey: "shared-key"),
            TestContext.Current.CancellationToken);

        // Reset adapter to a fresh pair so the second merge is logically distinct.
        FakeAggregate fresh = new(Guid.NewGuid(), "Fresh");
        FakeAggregate other = new(Guid.NewGuid(), "Other");
        FakeAdapter adapter2 = new(fresh, other);
        var sut2 = new EfMergeService<FakeAggregate>(adapter2, [], _factory, _guidGenerator, _clock);

        await Should.ThrowAsync<MergeException>(() =>
            sut2.MergeAsync(
                new MergeRequest(fresh.Id, other.Id, MergeFieldChoices.Empty, Reason: "second", IdempotencyKey: "shared-key"),
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task MergeAsync_Live_OrdersRewritersByDescription()
    {
        FakeAggregate survivor = new(SurvivorId, "Survivor");
        FakeAggregate loser = new(LoserId, "Loser");
        FakeAdapter adapter = new(survivor, loser);
        var executionLog = new List<string>();
        FakeRewriter zebra = new(rewriteCount: 1, countCount: 1, "zebra.RefId", executionLog);
        FakeRewriter alpha = new(rewriteCount: 1, countCount: 1, "alpha.RefId", executionLog);
        var sut = new EfMergeService<FakeAggregate>(adapter, [zebra, alpha], _factory, _guidGenerator, _clock);

        await sut.MergeAsync(new MergeRequest(SurvivorId, LoserId, MergeFieldChoices.Empty), TestContext.Current.CancellationToken);

        executionLog.ShouldBe(["alpha.RefId", "zebra.RefId"]);
    }

    private sealed class FakeAggregate(Guid id, string name) : AggregateRoot, IMergeable<FakeAggregate>
    {
        public Guid? MergedIntoId { get; set; }
        public DateTimeOffset? MergedAt { get; set; }
        public string Name { get; set; } = name;
        public int MergeFromCalls { get; private set; }

        // Need to set Id since AggregateRoot.Id has a private setter — use init via constructor.
        public new Guid Id { get; init; } = id;

        public IReadOnlyList<FieldConflict> GetConflicts(FakeAggregate loser)
        {
            // Always one conflict for "Name" so the test can assert .Conflicts is populated.
            return [new FieldConflict("Name", Name, loser.Name, WinnerSide.Survivor)];
        }

        public void MergeFrom(FakeAggregate loser, MergeFieldChoices choices)
        {
            MergeFromCalls++;
            if (choices.ResolveOrDefault("Name", WinnerSide.Survivor) == WinnerSide.Loser)
            {
                Name = loser.Name;
            }
        }
    }

    private sealed class FakeAdapter(FakeAggregate? survivor, FakeAggregate? loser) : IMergeableAggregateAdapter<FakeAggregate>
    {
        public int PersistCalls { get; private set; }
        public int TombstoneCalls { get; private set; }
        public Guid AppliedTombstoneSurvivorId { get; private set; }
        public DateTimeOffset AppliedTombstoneAt { get; private set; }
        public int ChainCollapseCalls { get; private set; }

        public Task<FakeAggregate?> LoadAsync(Guid id, CancellationToken cancellationToken)
        {
            if (id == survivor?.Id)
            {
                return Task.FromResult<FakeAggregate?>(survivor);
            }

            if (id == loser?.Id)
            {
                return Task.FromResult<FakeAggregate?>(loser);
            }

            return Task.FromResult<FakeAggregate?>(null);
        }

        public Task PersistMergedPairAsync(FakeAggregate s, FakeAggregate l, CancellationToken ct)
        {
            PersistCalls++;
            return Task.CompletedTask;
        }

        public void ApplyTombstone(FakeAggregate l, Guid survivorId, DateTimeOffset mergedAt)
        {
            TombstoneCalls++;
            AppliedTombstoneSurvivorId = survivorId;
            AppliedTombstoneAt = mergedAt;
            l.MergedIntoId = survivorId;
            l.MergedAt = mergedAt;
        }

        public Task<int> CollapseChainTombstonesAsync(Guid newSurvivor, Guid oldSurvivor, CancellationToken ct)
        {
            ChainCollapseCalls++;
            return Task.FromResult(0);
        }
    }

    private sealed class FakeRewriter(
        int rewriteCount,
        int countCount,
        string description = "fake.RefId",
        List<string>? executionLog = null) : IReferenceRewriter<FakeAggregate>
    {
        public string Description { get; } = description;
        public int RewriteCalls { get; private set; }
        public int CountCalls { get; private set; }

        public Task<int> RewriteAsync(Guid s, Guid l, CancellationToken ct)
        {
            RewriteCalls++;
            executionLog?.Add(Description);
            return Task.FromResult(rewriteCount);
        }

        public Task<int> CountAsync(Guid s, Guid l, CancellationToken ct)
        {
            CountCalls++;
            return Task.FromResult(countCount);
        }
    }

    private sealed class InMemoryFactory : IDbContextFactory<MergeableDbContext>
    {
        // Single shared in-memory database per test instance so idempotency replay tests
        // can re-read what the first call wrote.
        private readonly DbContextOptions<MergeableDbContext> _options =
            new DbContextOptionsBuilder<MergeableDbContext>()
                .UseInMemoryDatabase($"mergeable-tests-{Guid.NewGuid()}")
                .ConfigureWarnings(w =>
                    w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;

        public MergeableDbContext CreateDbContext() => new(_options);
        public Task<MergeableDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new MergeableDbContext(_options));
    }
}
