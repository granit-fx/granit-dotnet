# Granit.Documents.BackgroundJobs

Recurring background jobs for the Granit.Documents module: orphan blob cleanup
(F9.1), empty-trash retention (F9.2), and tenant quota recompute (F9.3).

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Documents.BackgroundJobs
```

## Recurring schedule

| Job | Cron | Purpose |
| --- | --- | --- |
| `documents-orphan-cleanup` | `0 * * * *` (hourly) | Cleans blobs stuck in `Pending` / `Uploading` after 24 h. |
| `documents-empty-trash` | `0 3 * * *` (nightly 03:00 UTC) | Promotes trashed documents older than `TrashRetentionDays` to `PermanentlyDeleted`. |
| `documents-quota-recompute` | `0 4 * * 0` (Sundays 04:00 UTC) | Reconciles `TenantStorageQuota.UsageBytes` with actual version-sum. |

All three handlers delegate to `IDocumentMaintenanceService`, registered by
`Granit.Documents.EntityFrameworkCore`. Hosts must reference the EFC companion
package for the jobs to do real work.
