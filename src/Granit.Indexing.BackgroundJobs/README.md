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
public sealed class RebuildController(
    IBackgroundJobDispatcher dispatcher,
    IPermissionChecker permissionChecker,
    ICurrentTenant currentTenant)
{
    public async Task TriggerAsync(CancellationToken ct)
    {
        // Tenant-scoped — caller must hold Indexing.Rebuild.Execute.
        if (!await permissionChecker.IsGrantedAsync(IndexingRebuildPermissions.Rebuild.Execute))
            throw new ForbiddenException();

        await dispatcher.PublishAsync(new RebuildIndexJob<Guid>(currentTenant.Id), cancellationToken: ct);
    }
}
```

`RebuildIndexJob` is on-demand — it has no `[RecurringJob]` attribute and is NOT
auto-scheduled. Hosts that want a recurring rebuild (e.g. nightly re-tokenisation)
wire their own cron-driven trigger that emits the job.

> [!IMPORTANT]
> **Authorization is host-enforced.** The framework ships the canonical permission
> names in `IndexingRebuildPermissions` but does **not** wire a runtime check inside
> the handler — by the time the Wolverine handler runs, the originating HTTP context
> is gone and the only principal information is what the dispatcher captured in the
> envelope. **You MUST enforce the permission at the dispatch site.** Without that
> check, any in-process code path that obtains `IBackgroundJobDispatcher` can rebuild
> any tenant.
>
> Two grants ship:
>
> - `Indexing.Rebuild.Execute` — tenant-scoped rebuild (pass the caller's own tenant id).
> - `Indexing.Rebuild.ExecuteGlobal` — cross-tenant rebuild (`TenantId: null`). Restrict
>   to host-side operators; this scans the entire dataset and is the most expensive
>   operation the module exposes.

## Resource budget

The rebuild iterates every key emitted by the source and calls `IIndexer<TKey>.IndexAsync`
once per entry — which in hosts wiring `Granit.Indexing.Embeddings` triggers one LLM
embedding call per entry. To bound the blast radius of a runaway or hostile dispatch:

| Option | Default | Purpose |
| ------ | ------- | ------- |
| `MaxEntriesPerRun` | `null` (unbounded) | Hard cap on entries processed in a single run. Set this in production. |
| `MaxRunDuration` | `null` (unbounded) | Wall-clock budget. The service checkpoints + throws `RebuildBudgetExceededException` once exceeded; Wolverine retries from the checkpoint. |
| `MaxConsecutiveFailures` | `50` | Circuit-breaker on per-key faults. |

When either budget is hit the checkpoint is preserved and a typed exception
(`RebuildBudgetExceededException`) is raised. Hosts wire this to a Wolverine retry
policy that re-dispatches the job after a cool-down.

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
      "MaxConsecutiveFailures": 50,
      "MaxEntriesPerRun": 1000000,
      "MaxRunDurationSeconds": 3600
    }
  }
}
```

## Operational events

Every rebuild raises in-process domain events on `ILocalEventBus` so audit / SIEM
subscribers correlate dispatch-time identity with the run lifecycle:

- `IndexRebuildStartedEvent` — at the very start, before reading the first key.
- `IndexRebuildCompletedEvent` — on successful end-of-stream.
- `IndexRebuildAbortedEvent` — when `MaxConsecutiveFailures`, `MaxEntriesPerRun`,
  or `MaxRunDuration` cuts the run short, or on cancellation. Carries the abort reason.

Each event carries `TenantId`, `SourceName`, `TKey` discriminator, processed count,
and (when available) the dispatching `UserId` — wire it into `Granit.Auditing` for a
GDPR-grade processing log.
