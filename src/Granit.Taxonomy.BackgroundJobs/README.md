# Granit.Taxonomy.BackgroundJobs

Background jobs for the Granit Taxonomy module. Catches `TagAssignment` and
`CategoryAssignment` rows that escaped the synchronous T5.1 cleanup handler.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Taxonomy.BackgroundJobs
```

## Jobs

| Job | Cron | Description |
| --- | --- | --- |
| `taxonomy-orphan-assignment-cleanup` | `0 3 * * *` (daily 03:00 UTC) | Sweeps assignment rows whose target aggregate no longer exists. |

Cron schedules are overridable via `BackgroundJobs:Jobs:{job-name}` in configuration.

## Probe registration

The sweep consults a per-target-type `ITaggableExistenceProbe` to decide whether
an assignment is orphaned. Hosts register one probe per taggable aggregate:

```csharp
services.AddTaggableExistenceProbe<Document, DocumentExistenceProbe>();
```

Without a registered probe, the sweep treats the target as still-existing — the
safe default that preserves data when no oracle is available.

## Dependencies

- `Granit.BackgroundJobs`
- `Granit.Taxonomy`

## Documentation

See the [full documentation](https://granit-fx.dev).
