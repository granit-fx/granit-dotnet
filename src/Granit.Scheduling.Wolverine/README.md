# Granit.Scheduling.Wolverine

Wolverine integration for Granit.Scheduling. Provides durable one-shot scheduling via `IMessageBus.ScheduleAsync`, automatic status tracking middleware (Executed/Failed), and tenant/user context propagation through the Wolverine outbox.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Scheduling.Wolverine
```

## Dependencies

- `Granit.Scheduling`
- `Granit.Wolverine`

## Documentation

See the [full documentation](https://granit-fx.dev).
