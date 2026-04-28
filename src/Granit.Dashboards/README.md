# Granit.Dashboards

Dashboard composition runtime for Granit. Hosts the
`IDashboardDefinitionRegistry` implementation and the
`AddDashboardDefinition<T>()` DI extension. Future home of the persisted `Dashboard`
aggregate, import / re-sync logic, and the real-time hub for IoT-style live tiles.

Reference [Granit.Dashboards.Abstractions](../Granit.Dashboards.Abstractions/) to
declare dashboards or widgets; reference this package only from hosts that execute,
persist, or expose them.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Dashboards
```

## Dependencies

- `Granit`
- `Granit.Dashboards.Abstractions`

## Documentation

See the [full documentation](https://granit-fx.dev).
