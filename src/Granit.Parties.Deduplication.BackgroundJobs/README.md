# Granit.Parties.Deduplication.BackgroundJobs

Recurring background scan that materialises Party duplicate candidates into a
review table. Iterates active tenants, runs the
[`IPartyDuplicateDetector`](../Granit.Parties.Deduplication/README.md) pipeline
per party, and upserts the resulting candidate pairs through
`IDuplicateCandidateSink` so the admin endpoints (#1301) can surface them.

| Cron | What |
| ---- | ---- |
| `0 3 * * *` | Daily at 03:00 — full tenant sweep |

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Parties.Deduplication.BackgroundJobs
```

## Setup

```csharp
[DependsOn(typeof(GranitPartiesDeduplicationBackgroundJobsModule))]
public class AppModule : GranitModule { }
```

The module registers:

- `IDuplicateCandidateSink` → `EfDuplicateCandidateSink` (writes to the
  `parties_duplicate_candidates` table managed by `PartiesDbContext`)
- `PartyDuplicateScanService` (per-tenant orchestrator)
- `PartyDuplicateScanJob` (Wolverine recurring job; cron above)
- `PartiesDeduplicationMetrics` (`granit.parties.deduplication.scan.*`)

## Persistence

The `parties_duplicate_candidates` table is owned by `PartiesDbContext` (in
`Granit.Parties.EntityFrameworkCore`) so its migration ships with the rest of
the Parties module. Pairs are stored ordered (`PartyId < CandidateId`) — the
unique index `(TenantId, PartyId, CandidateId, Tier)` rejects duplicates
regardless of which end "found" the pair first. Dismissed pairs (admin marked
"not a duplicate") are never reinserted across re-scans.

PostgreSQL deployments can opt into a partial index for the
"list pending duplicates" admin query:

```csharp
protected override void Up(MigrationBuilder migrationBuilder) =>
    migrationBuilder.AddPartyDuplicateCandidatesPendingPartialIndex();

protected override void Down(MigrationBuilder migrationBuilder) =>
    migrationBuilder.RemovePartyDuplicateCandidatesPendingPartialIndex();
```

## Documentation

See the [full documentation](https://granit-fx.dev).
