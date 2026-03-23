# Granit.Privacy.BackgroundJobs

Background jobs for the Granit Privacy module. Automates deletion deadline enforcement.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Privacy.BackgroundJobs
```

## Jobs

| Job | Cron | Description |
| --- | --- | --- |
| `privacy-deletion-deadline-enforcer` | `0 2 * * *` (daily at 2 AM) | Enforces deletion deadlines for deferred GDPR requests |

Safety-net job that catches any deferred deletion requests the `GdprDeletionSaga`
might have missed (e.g., due to outbox issues).

Cron schedules are overridable via `BackgroundJobs:Jobs:{job-name}` in configuration.

## Dependencies

- `Granit.BackgroundJobs`
- `Granit.Privacy`

## Documentation

See the [full documentation](https://granit-fx.dev).
