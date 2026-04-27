# Granit.Webhooks.Notifications

Notification bridge for `Granit.Webhooks`. Alerts subscribers (typically tenant
administrators) when an outbound webhook subscription crosses its consecutive
delivery-failure threshold so they can investigate before events are silently
lost or the subscription is auto-suspended. Ships embedded HTML templates in
**English and French** (overridable at runtime through the `Granit.Templating`
admin API).

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Webhooks.Notifications
```

## Dependencies

- `Granit.Webhooks`
- `Granit.Notifications.Abstractions`
- `Granit.Templating`

## Notification types

| Name | Severity | Default channels |
| ---- | -------- | ---------------- |
| `webhooks.delivery_failure_threshold` | Warning | Email + InApp |

Recipients are resolved via the standard notifications subscription system —
administrators opt in through the admin UI; the bridge itself owns no
tenant-admin lookup logic.

## Documentation

See the [full documentation](https://granit-fx.dev).
