# Granit.Bff.BackgroundJobs

Background jobs for the Granit BFF module. Automates expired session cleanup
for EF Core-backed deployments.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Bff.BackgroundJobs
```

## Jobs

| Job | Cron | Description |
| --- | --- | --- |
| `bff-expired-session-cleanup` | `*/15 * * * *` (every 15 min) | Purges expired BFF sessions from the database |

SQL databases have no native TTL — this job reclaims storage by deleting
expired sessions. Only needed for EF Core-backed BFF deployments (not Redis).

Cron schedules are overridable via `BackgroundJobs:Jobs:{job-name}` in configuration.

## Dependencies

- `Granit.BackgroundJobs`
- `Granit.Bff.EntityFrameworkCore`

## Documentation

See the [full documentation](https://granit-fx.dev).
