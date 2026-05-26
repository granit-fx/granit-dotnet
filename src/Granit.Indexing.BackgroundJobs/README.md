# Granit.Indexing.BackgroundJobs

Background jobs add-on for `Granit.Indexing`. Ships an on-demand `RebuildIndexJob<TKey>`
with durable crash-resume via a configurable checkpoint store.

## When to wire it in

The default indexing pipeline keeps the index in sync via lifecycle events. Reach for
the rebuild job when:

- You deployed a new tokenizer / analyzer and need to re-index the corpus.
- You onboarded a new tenant and want to backfill the existing rows.
- An operator manually triggers a full rebuild after a data-quality incident.

## Registration

```csharp
builder.Services.AddGranitIndexing();
builder.Services.AddGranitBackgroundJobs();
builder.Services.AddGranitIndexingBackgroundJobs();

// One per TKey you want to rebuild:
builder.Services.AddGranitIndexingRebuildSource<Guid, MyDocumentSource>();

// Persistent checkpoints (recommended in prod — survives worker restarts):
builder.Services.AddGranitIndexingEntityFrameworkCoreCheckpointStore<Guid>();
```

Without `AddGranitIndexingEntityFrameworkCoreCheckpointStore<TKey>()` the framework
falls back to `InMemoryRebuildCheckpointStore<TKey>` — fine for dev / single-process,
but state is lost on restart.

## Triggering a rebuild

```csharp
public sealed class RebuildController(IBackgroundJobDispatcher dispatcher)
{
    public Task TriggerAsync(Guid tenantId, CancellationToken ct) =>
        dispatcher.PublishAsync(new RebuildIndexJob<Guid>(tenantId), cancellationToken: ct);
}
```

`RebuildIndexJob` carries a `null` cron — it's NOT auto-scheduled. Hosts that want a
recurring rebuild (e.g. nightly re-tokenisation) wire their own cron-driven trigger
that emits the job.

## Crash recovery

The rebuild service persists a checkpoint after every
`Indexing:BackgroundJobs:CheckpointBatchSize` entries (default 100). On crash, the
next dispatch reads the checkpoint and resumes past it — no duplicate indexing, no
missing rows, provided your `IIndexedEntrySource.EnumerateKeysAsync` honours the
`resumeAfter` contract (return keys strictly past the checkpoint, same order across
calls).

A run that exceeds `MaxConsecutiveFailures` (default 50) aborts and the checkpoint is
preserved so a follow-up trigger resumes from where the corruption started.

## Configuration

```json
{
  "Indexing": {
    "BackgroundJobs": {
      "CheckpointBatchSize": 100,
      "MaxConsecutiveFailures": 50
    }
  }
}
```
