# Granit.BackgroundJobs.Notifications

Notification bridge for `Granit.BackgroundJobs`. Alerts platform / tenant administrators
when a recurring job fails N consecutive runs (default: 3) so SLA-critical batches —
compliance reports, billing rollups, data exports — cannot fail silently for hours
before being noticed. Ships embedded HTML templates in **English and French**,
overridable at runtime through the `Granit.Templating` admin API.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.BackgroundJobs.Notifications
```

## Dependencies

- `Granit.BackgroundJobs`
- `Granit.Notifications`
- `Granit.Templating`

## Notification types

| Name | Default channels | Severity | Trigger |
| ---- | ---------------- | -------- | ------- |
| `jobs.recurring_failing` | Email + InApp | Warning | `BackgroundJobFailureThresholdExceededEto` (3 consecutive failures) |

Recipients are resolved through `INotificationPublisher.PublishToSubscribersAsync` —
admins opt in via the notifications admin UI rather than being hardcoded into options.

## Documentation

See the [full documentation](https://granit-fx.dev).
