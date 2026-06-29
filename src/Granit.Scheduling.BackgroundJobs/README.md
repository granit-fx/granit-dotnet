# Granit.Scheduling.BackgroundJobs

Background jobs for Granit.Scheduling. Safety-net catch-up job that detects overdue scheduled actions and re-dispatches them via Wolverine, ensuring no scheduled action is silently lost after broker restarts.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Scheduling.BackgroundJobs
```

## Dependencies

- `Granit.BackgroundJobs`
- `Granit.Scheduling`
- `Granit.Scheduling.Wolverine`

## Documentation

See the [full documentation](https://granit-fx.dev).
