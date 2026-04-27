# Granit.Metering.Notifications

Notification bridge for `Granit.Metering`. When a tenant's usage approaches the
configured warning threshold (default 80%) or reaches the hard quota limit, this
package publishes a `metering.quota_threshold_reached` or `metering.quota_exceeded`
notification to every administrator subscribed to that type, so they can upgrade or
rebalance before throttling kicks in. Ships embedded HTML templates in **English and
French** for both notification types (overridable at runtime through the
`Granit.Templating` admin API).

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Metering.Notifications
```

## Dependencies

- `Granit.Metering`
- `Granit.Notifications`
- `Granit.Templating`

## Notification types

| Notification name | Trigger | Default channels | Severity |
| ----------------- | ------- | ---------------- | -------- |
| `metering.quota_threshold_reached` | `QuotaThresholdReachedEto` (default 80% of limit) | Email, InApp | Warning |
| `metering.quota_exceeded` | `QuotaExceededEto` (>= 100% of limit) | Email, InApp | Error |

Recipients are resolved via `INotificationPublisher.PublishToSubscribersAsync`:
administrators opt in through the notifications admin UI and are naturally
tenant-scoped by the subscription engine — no hardcoded email lists.

## Documentation

See the [full documentation](https://granit-fx.dev).
