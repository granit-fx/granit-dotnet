using System.Diagnostics.Metrics;
using Granit.Events;
using Granit.Indexing.BackgroundJobs.Diagnostics;
using Granit.Indexing.BackgroundJobs.Events;
using Granit.Indexing.BackgroundJobs.Exceptions;
using Granit.Indexing.BackgroundJobs.Internal;
using Granit.Indexing.BackgroundJobs.Options;
using Granit.Indexing.BackgroundJobs.Services;
using Granit.Users;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Indexing.BackgroundJobs.Tests;

public sealed class RebuildIndexServiceTests
{
    private static readonly Guid TenantA = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    [Fact]
    public async Task ExecuteAsync_indexes_every_key_emitted_by_the_source_then_clears_the_checkpoint()
    {
        // Happy path: 3-key source runs to completion, every IndexAsync is called, and
        // the checkpoint is cleared so a future run starts fresh.
        Harness h = new();
        h.WithKeys(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        RebuildIndexService<Guid> service = h.Build();
        await service.ExecuteAsync(TenantA, TestContext.Current.CancellationToken);

        await h.Indexer.Received(3).IndexAsync(Arg.Any<IndexedEntry<Guid>>(), Arg.Any<CancellationToken>());
        (await h.Checkpoints.GetLastCheckpointAsync(TenantA, h.Source.Name, TestContext.Current.CancellationToken))
            .ShouldBe(default);
    }

    [Fact]
    public async Task ExecuteAsync_resumes_past_the_stored_checkpoint_so_no_key_is_re_indexed()
    {
        // Crash-recovery contract: when a checkpoint exists, the service skips the
        // already-processed prefix by passing the checkpoint to the source's
        // resumeAfter. The source MUST honour that — and we verify it does by
        // asserting only the keys past the checkpoint were indexed.
        Harness h = new();
        var alreadyDone = Guid.NewGuid();
        var k1 = Guid.NewGuid();
        var k2 = Guid.NewGuid();
        h.WithKeysAndResumeContract(checkpoint: alreadyDone, keysAfter: [k1, k2]);
        await h.Checkpoints.SetCheckpointAsync(TenantA, h.Source.Name, alreadyDone, TestContext.Current.CancellationToken);

        RebuildIndexService<Guid> service = h.Build();
        await service.ExecuteAsync(TenantA, TestContext.Current.CancellationToken);

        // The source must have been called with the checkpoint as resumeAfter.
        h.Source.Received(1).EnumerateKeysAsync(TenantA, alreadyDone, Arg.Any<CancellationToken>());
        // Only the post-checkpoint keys were indexed.
        await h.Indexer.Received(2).IndexAsync(Arg.Any<IndexedEntry<Guid>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_writes_checkpoints_every_BatchSize_entries()
    {
        // The checkpoint cadence is the crash-recovery granularity. A 5-key run with
        // batch size 2 should emit checkpoints after key 2 and key 4 (plus the
        // ClearAsync at the end). Locks the cadence so a future refactor that
        // accidentally writes every key (chatty) or every full run (lossy) is caught.
        Harness h = new();
        h.Options.CheckpointBatchSize = 2;
        h.WithKeys(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        using MetricCollector<long> checkpointCollector = new(
            h.MeterFactory.Meter, "granit.indexing.background_jobs.checkpoints.written");

        RebuildIndexService<Guid> service = h.Build();
        await service.ExecuteAsync(TenantA, TestContext.Current.CancellationToken);

        // 5 keys, batch 2 → checkpoints after key 2 and after key 4 = 2 writes.
        checkpointCollector.GetMeasurementSnapshot().Sum(m => m.Value).ShouldBe(2);
    }

    [Fact]
    public async Task ExecuteAsync_skips_entries_whose_BuildEntryAsync_returns_null()
    {
        // A resource deleted between enumeration and build is a real-world race. The
        // service must skip it without aborting the run.
        Harness h = new();
        var present = Guid.NewGuid();
        var deleted = Guid.NewGuid();
        h.WithKeys(present, deleted);
        h.Source.BuildEntryAsync(deleted, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IndexedEntry<Guid>?>(null));

        RebuildIndexService<Guid> service = h.Build();
        await service.ExecuteAsync(TenantA, TestContext.Current.CancellationToken);

        await h.Indexer.Received(1).IndexAsync(Arg.Any<IndexedEntry<Guid>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_aborts_after_MaxConsecutiveFailures_and_preserves_checkpoint()
    {
        // Bug-rollout protection: when every entry fails (e.g. broken serializer), the
        // service stops blasting the failure metric channel and preserves the
        // checkpoint so a fix-then-retrigger doesn't lose the prior progress.
        Harness h = new();
        h.Options.CheckpointBatchSize = 1; // checkpoint after the first success
        h.Options.MaxConsecutiveFailures = 3;
        var ok = Guid.NewGuid();
        var f1 = Guid.NewGuid();
        var f2 = Guid.NewGuid();
        var f3 = Guid.NewGuid();
        h.WithKeys(ok, f1, f2, f3);

        // Indexer throws for the failing keys.
        IndexedEntry<Guid> okEntry = new() { Key = ok, TenantId = TenantA, Content = "ok", };
        h.Source.BuildEntryAsync(ok, Arg.Any<CancellationToken>()).Returns(Task.FromResult<IndexedEntry<Guid>?>(okEntry));
        h.Source.BuildEntryAsync(Arg.Is<Guid>(g => g == f1 || g == f2 || g == f3), Arg.Any<CancellationToken>())
            .Returns<Task<IndexedEntry<Guid>?>>(_ => throw new InvalidOperationException("simulated build fault"));

        RebuildIndexService<Guid> service = h.Build();
        await Should.ThrowAsync<InvalidOperationException>(() =>
            service.ExecuteAsync(TenantA, TestContext.Current.CancellationToken));

        // The checkpoint must hold the last SUCCESSFUL key — not the failing ones —
        // so a retry resumes past it without re-trying the corrupt rows.
        (await h.Checkpoints.GetLastCheckpointAsync(TenantA, h.Source.Name, TestContext.Current.CancellationToken))
            .ShouldBe(ok);
    }

    [Fact]
    public async Task ExecuteAsync_publishes_Started_and_Completed_events_on_success()
    {
        // GDPR-grade audit trail: every rebuild lifecycle transition publishes a domain
        // event on ILocalEventBus so audit subscribers correlate dispatch-time identity
        // (DispatchedByUserId) with the run outcome.
        Harness h = new();
        h.WithKeys(Guid.NewGuid(), Guid.NewGuid());

        RebuildIndexService<Guid> service = h.Build();
        await service.ExecuteAsync(TenantA, TestContext.Current.CancellationToken);

        IndexRebuildStartedEvent started = h.EventBus.Published.OfType<IndexRebuildStartedEvent>().ShouldHaveSingleItem();
        started.TenantId.ShouldBe(TenantA);
        started.SourceName.ShouldBe("documents");
        started.ResumedFromCheckpoint.ShouldBeFalse();
        started.DispatchedByUserId.ShouldBe("user-7");

        IndexRebuildCompletedEvent completed = h.EventBus.Published.OfType<IndexRebuildCompletedEvent>().ShouldHaveSingleItem();
        completed.EntriesIndexed.ShouldBe(2);
        completed.EntriesSkipped.ShouldBe(0);
        completed.EntriesFailed.ShouldBe(0);
        completed.DispatchedByUserId.ShouldBe("user-7");
    }

    [Fact]
    public async Task ExecuteAsync_publishes_Aborted_event_when_MaxConsecutiveFailures_trips()
    {
        // The abort path emits IndexRebuildAbortedEvent with a stable Reason — required
        // for SIEM rules that distinguish circuit-breaker trips from budget cutoffs.
        Harness h = new();
        h.Options.CheckpointBatchSize = 1;
        h.Options.MaxConsecutiveFailures = 2;
        var f1 = Guid.NewGuid();
        var f2 = Guid.NewGuid();
        h.WithKeys(f1, f2);
        h.Source.BuildEntryAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns<Task<IndexedEntry<Guid>?>>(_ => throw new InvalidOperationException("boom"));

        RebuildIndexService<Guid> service = h.Build();
        await Should.ThrowAsync<InvalidOperationException>(() =>
            service.ExecuteAsync(TenantA, TestContext.Current.CancellationToken));

        IndexRebuildAbortedEvent aborted = h.EventBus.Published.OfType<IndexRebuildAbortedEvent>().ShouldHaveSingleItem();
        aborted.Reason.ShouldBe("max_consecutive_failures");
        h.EventBus.Published.OfType<IndexRebuildCompletedEvent>().ShouldBeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_throws_RebuildBudgetExceeded_when_MaxEntriesPerRun_is_hit_and_preserves_checkpoint()
    {
        // Denial-of-Wallet defense: a host that caps entries-per-run gets a deterministic
        // cutoff, the checkpoint is preserved so the retry resumes, and a typed exception
        // signals to Wolverine that this is a budget yield (not a fault).
        Harness h = new();
        h.Options.CheckpointBatchSize = 100; // do not flush mid-batch
        h.Options.MaxEntriesPerRun = 2;
        var k1 = Guid.NewGuid();
        var k2 = Guid.NewGuid();
        var k3 = Guid.NewGuid();
        h.WithKeys(k1, k2, k3);

        RebuildIndexService<Guid> service = h.Build();
        RebuildBudgetExceededException ex = await Should.ThrowAsync<RebuildBudgetExceededException>(() =>
            service.ExecuteAsync(TenantA, TestContext.Current.CancellationToken));

        ex.Reason.ShouldBe("max_entries_per_run");
        ex.ProcessedCount.ShouldBe(2);

        // Checkpoint preserved at last successful key so retry resumes past k2.
        (await h.Checkpoints.GetLastCheckpointAsync(TenantA, h.Source.Name, TestContext.Current.CancellationToken))
            .ShouldBe(k2);

        IndexRebuildAbortedEvent aborted = h.EventBus.Published.OfType<IndexRebuildAbortedEvent>().ShouldHaveSingleItem();
        aborted.Reason.ShouldBe("max_entries_per_run");
    }

    [Fact]
    public async Task ExecuteAsync_throws_RebuildBudgetExceeded_when_MaxRunDuration_elapses()
    {
        // Wall-clock budget cutoff. We advance the FakeTimeProvider between keys so the
        // service notices the budget overrun on the first per-key budget check.
        Harness h = new();
        h.Options.CheckpointBatchSize = 100;
        h.Options.MaxRunDurationSeconds = 30;
        var k1 = Guid.NewGuid();
        var k2 = Guid.NewGuid();
        h.WithKeys(k1, k2);
        // Advance the clock when BuildEntryAsync(k1) is called so the post-key check trips.
        h.Source.BuildEntryAsync(k1, Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                h.TimeProvider.Advance(TimeSpan.FromMinutes(1));
                return Task.FromResult<IndexedEntry<Guid>?>(new IndexedEntry<Guid> { Key = k1, TenantId = TenantA, Content = "x" });
            });

        RebuildIndexService<Guid> service = h.Build();
        RebuildBudgetExceededException ex = await Should.ThrowAsync<RebuildBudgetExceededException>(() =>
            service.ExecuteAsync(TenantA, TestContext.Current.CancellationToken));

        ex.Reason.ShouldBe("max_run_duration");
        h.EventBus.Published.OfType<IndexRebuildAbortedEvent>().ShouldHaveSingleItem()
            .Reason.ShouldBe("max_run_duration");
    }

    private sealed class Harness
    {
        public IIndexer<Guid> Indexer { get; } = Substitute.For<IIndexer<Guid>>();
        public IIndexedEntrySource<Guid> Source { get; } = Substitute.For<IIndexedEntrySource<Guid>>();
        public InMemoryRebuildCheckpointStore<Guid> Checkpoints { get; } = new();
        public IndexingBackgroundJobsOptions Options { get; } = new();
        public TestMeterFactory MeterFactory { get; } = new();
        public IndexingBackgroundJobsMetrics Metrics { get; }
        public RecordingEventBus EventBus { get; } = new();
        public ICurrentUserService CurrentUser { get; } = Substitute.For<ICurrentUserService>();
        public FakeTimeProvider TimeProvider { get; } = new();

        public Harness()
        {
            Source.Name.Returns("documents");
            Metrics = new IndexingBackgroundJobsMetrics(MeterFactory);
            CurrentUser.UserId.Returns("user-7");
        }

        public void WithKeys(params Guid[] keys)
        {
            Source.EnumerateKeysAsync(Arg.Any<Guid?>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                .Returns(ci => AsAsync(keys));
            foreach (Guid k in keys)
            {
                Source.BuildEntryAsync(k, Arg.Any<CancellationToken>())
                    .Returns(Task.FromResult<IndexedEntry<Guid>?>(new IndexedEntry<Guid>
                    {
                        Key = k,
                        TenantId = TenantA,
                        Content = $"body-{k}",
                    }));
            }
        }

        public void WithKeysAndResumeContract(Guid checkpoint, Guid[] keysAfter)
        {
            // The source returns the keysAfter ONLY when the resumeAfter argument is the
            // checkpoint we set up. This lets the test assert the service actually
            // forwards the checkpoint — the contract from IIndexedEntrySource.
            Source.EnumerateKeysAsync(TenantA, checkpoint, Arg.Any<CancellationToken>())
                .Returns(ci => AsAsync(keysAfter));
            // Default for any other resumeAfter: empty (so a wrong forward returns no
            // keys and the test fails on assert count).
            Source.EnumerateKeysAsync(TenantA, Arg.Is<Guid>(g => g != checkpoint), Arg.Any<CancellationToken>())
                .Returns(ci => AsAsync([]));
            foreach (Guid k in keysAfter)
            {
                Source.BuildEntryAsync(k, Arg.Any<CancellationToken>())
                    .Returns(Task.FromResult<IndexedEntry<Guid>?>(new IndexedEntry<Guid>
                    {
                        Key = k,
                        TenantId = TenantA,
                        Content = $"body-{k}",
                    }));
            }
        }

        public RebuildIndexService<Guid> Build() => new(
            Indexer,
            Source,
            Checkpoints,
            Microsoft.Extensions.Options.Options.Create(Options),
            Metrics,
            EventBus,
            CurrentUser,
            TimeProvider,
            NullLogger<RebuildIndexService<Guid>>.Instance);

        private static async IAsyncEnumerable<Guid> AsAsync(IEnumerable<Guid> keys)
        {
            foreach (Guid k in keys)
            {
                yield return k;
                await Task.Yield();
            }
        }
    }

    private sealed class TestMeterFactory : IMeterFactory
    {
        public Meter Meter { get; } = new(IndexingBackgroundJobsMetrics.MeterName);
        public Meter Create(MeterOptions options) => Meter;
        public void Dispose() => Meter.Dispose();
    }

    private sealed class RecordingEventBus : ILocalEventBus
    {
        public List<object> Published { get; } = [];

        public Task PublishAsync<TEvent>(TEvent localEvent, CancellationToken cancellationToken = default)
            where TEvent : class
        {
            Published.Add(localEvent);
            return Task.CompletedTask;
        }
    }
}
