# Granit.Scheduling.Notifications

Notification bridge for `Granit.Scheduling`. Notifies tenant administrators when a
scheduled action (report, batch export, periodic sync, etc.) fails after exhausting
retries — preventing silent failures from going unnoticed for days. Ships embedded
HTML templates in **English and French**, overridable at runtime through the
`Granit.Templating` admin API.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Scheduling.Notifications
```

## Dependencies

- `Granit.Scheduling`
- `Granit.Notifications.Abstractions`
- `Granit.Templating`

## Notification types

| Name | Default channels | Severity | Trigger |
| ---- | ---------------- | -------- | ------- |
| `scheduling.action_failed` | Email + InApp | Warning | `ScheduledActionFailedEto` |

Recipients are resolved through `INotificationPublisher.PublishToSubscribersAsync` —
admins opt in via the notifications admin UI rather than being hardcoded into options.

## Documentation

See the [full documentation](https://granit-fx.dev).
