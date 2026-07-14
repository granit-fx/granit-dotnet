# Granit.DataExchange.BackgroundJobs

Background jobs for the Granit DataExchange module. Automates the GDPR retention sweep.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.DataExchange.BackgroundJobs
```

## Jobs

| Job | Cron | Description |
| --- | --- | --- |
| `data-exchange-retention-sweep` | `0 3 * * *` (daily at 3 AM) | Purges expired import/export files and job records, recovers stuck jobs |

The sweep runs six independent categories per execution, each capped at `SweepBatchSize`:

1. **Import file purge** — deletes the uploaded file behind terminal import jobs older than `ImportFileRetention`.
2. **Export file purge** — deletes the generated file behind completed export jobs older than `ExportFileRetention`.
3. **Stuck import recovery** — force-fails import jobs stranded in `Executing` beyond `StuckJobTimeout`.
4. **Stuck export recovery** — force-fails export jobs stranded in `Exporting` beyond `StuckJobTimeout`.
5. **Import record purge** — hard-deletes terminal import job rows older than `JobRecordRetention`.
6. **Export record purge** — hard-deletes terminal export job rows older than `JobRecordRetention`.

Each job within a category is processed independently: one failure (a transient blob-storage
error, a concurrency conflict) is logged and skipped rather than aborting the rest of the sweep —
the next scheduled run retries it.

Cron schedule is overridable via `BackgroundJobs:Jobs:{job-name}` in configuration.

## GDPR rationale

Uploaded import files and generated export files routinely carry personal data (raw source rows,
exported entity fields). Retaining them indefinitely — or retaining the job metadata describing
them — violates the storage-limitation principle (Art. 5(1)(e)). This sweep is the automated
enforcement point: once a job's outcome is known and its retention window elapses, the underlying
file and, later, the row itself are purged. Stuck-job recovery exists so that a crashed worker or
lost message can never leave a job permanently outside the terminal states the retention queries
key off of — without it, a stranded job's file would never become eligible for purge.

## Options

`DataExchange:Retention` (`DataExchangeRetentionOptions`):

| Option | Default | Description |
| --- | --- | --- |
| `ImportFileRetention` | 30 days | How long an uploaded import file is kept after the job reaches a terminal state. |
| `ExportFileRetention` | 7 days | How long a generated export file is kept after completion. |
| `JobRecordRetention` | 365 days | How long a terminal job's database row is kept before hard-delete. |
| `StuckJobTimeout` | 6 hours | How long a job may remain non-terminal before being treated as stranded. |
| `SweepBatchSize` | 500 | Maximum number of jobs processed per category, per run. |

## Dependencies

- `Granit.BackgroundJobs`
- `Granit.DataExchange`

A durable `IDataExchangeRetentionStore` must be registered — install
`Granit.DataExchange.EntityFrameworkCore` (`AddGranitDataExchangeEntityFrameworkCore`) for the EF
Core implementation; the default is a fail-fast null-object.

## Documentation

See the [full documentation](https://granit-fx.dev).
