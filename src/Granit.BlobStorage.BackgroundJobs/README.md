# Granit.BlobStorage.BackgroundJobs

Background jobs for the Granit BlobStorage module. Automates orphan blob cleanup.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.BlobStorage.BackgroundJobs
```

## Jobs

| Job | Cron | Description |
| --- | --- | --- |
| `blob-storage-orphan-cleanup` | `0 * * * *` (hourly) | Cleans up blobs stuck in Pending/Uploading for over 24 hours |

Cron schedules are overridable via `BackgroundJobs:Jobs:{job-name}` in configuration.

## Dependencies

- `Granit.BackgroundJobs`
- `Granit.BlobStorage`

## Documentation

See the [full documentation](https://granit-fx.dev).
