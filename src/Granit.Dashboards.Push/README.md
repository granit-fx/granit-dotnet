# Granit.Dashboards.Push

SSE-based push transport for Granit dashboards. Wires
`GET /dashboards/{id}/stream` to a per-tenant fan-out hub and exposes
`IWidgetPushPublisher` so producer modules emit live updates without
binding to a specific transport. ADR-043.

Reference this package from hosts that need live dashboard channels;
pull-only hosts skip it and degrade `RefreshHint.Realtime` widgets to
`Dynamic` cadence with no runtime breakage.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Dashboards.Push
```

## Dependencies

- `Granit`
- `Granit.Analytics.Abstractions`
- `Granit.Authorization`
- `Granit.Dashboards`
- `Granit.Dashboards.Abstractions`
- `Granit.Http.Abstractions`
- `Granit.MultiTenancy`
- `Granit.Timing`

## Documentation

See [ADR-043 — Dashboard push transport](https://granit-fx.dev/dotnet/architecture/adr/043-dashboard-push-transport/)
and the [full documentation](https://granit-fx.dev).
