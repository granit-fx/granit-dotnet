# Granit.EntityMerge.BackgroundJobs

Recurring background jobs for the [Granit.EntityMerge](../Granit.EntityMerge/README.md)
orchestrator. Currently ships one job:

| Job | Cron | What |
| --- | ---- | ---- |
| `MergeIdempotencyCleanupJob` | configurable | Deletes `granit.merge_idempotency` rows older than `EntityMergeOptions.IdempotencyRetention` (default 24 h) so the cache cannot grow unbounded and idempotency-key payloads do not linger past the configured retention window (GDPR Art. 5(1)(e) — storage limitation). |

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.EntityMerge.BackgroundJobs
```

## Setup

```csharp
[DependsOn(typeof(GranitEntityMergeBackgroundJobsModule))]
public class AppModule : GranitModule { }
```

The module's `ConfigureServices` registers the job + the
`IMergeIdempotencySweeper` + `MergeIdempotencyCleanupService` services.
Retention is bound from `appsettings.json`:

```json
{
  "EntityMerge": {
    "IdempotencyRetention": "1.00:00:00"
  }
}
```

## Dependencies

- `Granit.EntityMerge.EntityFrameworkCore` — the `EntityMergeDbContext` that owns
  the `granit.merge_idempotency` table the sweeper deletes from.
- `Granit.BackgroundJobs` — recurring-job infrastructure.

## Documentation

See the [full documentation](https://granit-fx.dev).
