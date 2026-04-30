# Granit.Dashboards.Push.WebSockets

WebSocket consumer for the Granit dashboards push transport. Sibling of
[Granit.Dashboards.Push](../Granit.Dashboards.Push/) (which ships the SSE
consumer); both reuse the same `IWidgetPushPublisher` producer contract and
the same in-memory hub. Opt-in — most hosts ship SSE only.

ADR-043 §1.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Dashboards.Push.WebSockets
```

## Dependencies

- `Granit`
- `Granit.Analytics.Abstractions`
- `Granit.Authorization`
- `Granit.Dashboards`
- `Granit.Dashboards.Abstractions`
- `Granit.Dashboards.Push`
- `Granit.Http.Abstractions`
- `Granit.MultiTenancy`
- `Granit.Timing`

## Documentation

See [ADR-043 — Dashboard push transport](https://granit-fx.dev/dotnet/architecture/adr/043-dashboard-push-transport/)
and the [full documentation](https://granit-fx.dev).
