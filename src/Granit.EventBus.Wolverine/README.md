# Granit.EventBus.Wolverine

Wolverine-backed event bus providers for the Granit framework.
Replaces the in-process defaults with `WolverineLocalEventBus` (local queue)
and `WolverineDistributedEventBus` (outbox-backed, at-least-once delivery).

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.EventBus.Wolverine
```

## Dependencies

- `Granit.EventBus`
- `Granit.Wolverine`

## Documentation

See the [full documentation](https://granit-fx.dev).
