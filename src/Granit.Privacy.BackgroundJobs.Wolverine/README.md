# Granit.Privacy.BackgroundJobs.Wolverine

Wolverine retry-with-cooldown policy for the personal-data export assembly
background job. Transient `PrivacyExportAssemblyException` retries at
1 min / 5 min / 15 min before the message moves to the dead-letter queue;
the mid-flight per-shard checkpoint store ensures retries resume past the
last committed shard rather than re-streaming every fragment from zero.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Privacy.BackgroundJobs.Wolverine
```

## Dependencies

- `Granit.Privacy.BackgroundJobs`
- `Granit.Wolverine`

## Documentation

See the [full documentation](https://granit-fx.dev).
