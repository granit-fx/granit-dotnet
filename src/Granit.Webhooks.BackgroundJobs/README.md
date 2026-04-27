# Granit.Webhooks.BackgroundJobs

Background jobs for the Granit Webhooks module. Automates signing-key rotation alerts.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Webhooks.BackgroundJobs
```

## Jobs

| Job | Cron | Description |
| --- | --- | --- |
| `webhooks-key-rotation-scan` | `0 7 * * *` (daily at 07:00 UTC) | Emits `WebhookSigningKeyRotationDueEto` for keys whose `ExpiresAt` is within `WebhooksOptions.RotationLeadTimeDays` (default 14 days). |

Idempotent — per-key dedupe via `WebhookSigningKey.LastRotationNotificationAt`; the
same key triggers at most one notification per calendar week.

Cron schedules are overridable via `BackgroundJobs:Jobs:{job-name}` in configuration.

## Dependencies

- `Granit.BackgroundJobs`
- `Granit.Webhooks`

## Documentation

See the [full documentation](https://granit-fx.dev).
