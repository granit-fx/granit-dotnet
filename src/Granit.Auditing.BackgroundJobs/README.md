# Granit.Auditing.BackgroundJobs

Distributed audit-log retention cleanup for [Granit.Auditing](../Granit.Auditing),
built on the [`Granit.BackgroundJobs`](../Granit.BackgroundJobs) recurring job
pattern.

Replaces the former per-pod `AuditingCleanupWorker` hosted service: on a
multi-replica Kubernetes deployment that worker ran on **every** pod and purged
the same table N× in parallel. The recurring job runs **once cluster-wide** per
schedule via the distributed scheduler.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Auditing.BackgroundJobs
```

Register `GranitAuditingBackgroundJobsModule` (auto-discovered). It depends on
`Granit.Auditing.EntityFrameworkCore` (the `IAuditingCleaner` implementation) and
`Granit.BackgroundJobs`.

## What it does

`AuditRetentionCleanupJob` (`[RecurringJob("0 2 * * *", "auditing-retention-cleanup")]`)
sweeps every `AuditCategory` and deletes entries older than its configured
retention (`AuditingOptions.*Retention`), in batches of
`AuditingOptions.CleanupBatchSize`.

Override the schedule via configuration:

```json
"BackgroundJobs": { "Jobs": { "auditing-retention-cleanup": "0 3 * * 0" } }
```

## Dependencies

- `Granit.BackgroundJobs`
- `Granit.Auditing.EntityFrameworkCore`
