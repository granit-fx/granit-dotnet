# Granit.Events

Default in-process event bus providers for the Granit framework.
Provides `InProcessLocalEventBus` and `InProcessDistributedEventBus` that
resolve `ILocalEventHandler<T>` / `IDistributedEventHandler<T>` from DI
and call them sequentially.

Replace with `Granit.Events.Wolverine` for durable, outbox-backed delivery.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Events
```

## Dependencies

- `Granit.Core`

## Documentation

See the [full documentation](https://granit-fx.dev).
