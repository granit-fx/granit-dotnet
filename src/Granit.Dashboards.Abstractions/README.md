# Granit.Dashboards.Abstractions

Inter-module contracts for `Granit.Dashboards`: `DashboardDefinition` declarative base
class, `WidgetDefinition` record hierarchy, layout / size value objects, registry
interface, and the three presentation-only widgets (`MarkdownWidgetDefinition`,
`ImageWidgetDefinition`, `TextWidgetDefinition`). Reference this package from any
module that declares dashboards or widgets (`Granit.Analytics`, future
`Granit.IoT.Dashboards`, ...); reference `Granit.Dashboards` only from hosts that
execute, persist, or expose them.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Dashboards.Abstractions
```

## Dependencies

- `Granit`

## Documentation

See the [full documentation](https://granit-fx.dev).
