# Granit.Notifications

Multi-channel notification engine for Granit. Provides `INotificationPublisher` for publishing
notifications, Wolverine-based transactional fan-out, `INotificationChannel` for pluggable
delivery channels, and automatic entity change tracking via `ITrackedEntity`.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Notifications
```

## Dependencies

- `Granit.Guids`
- `Granit.QueryEngine`
- `Granit.Timing`

## Documentation

See the [full documentation](https://granit-fx.dev).
