# Granit.QueryEngine.Abstractions

Contracts and core services for Granit.QueryEngine: declarative `QueryDefinition<T>`
fluent API, typed filters with operator inference, composable FilterGroups/presets/
DatePeriod, `PagedResult<T>`, `QueryRequest`, `QueryMetadata` for frontend
auto-configuration, plus `QueryEngineOptions` and OpenTelemetry metrics. Modules that
declare queries reference this package; hosts that execute them add
`Granit.QueryEngine.EntityFrameworkCore`.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.QueryEngine.Abstractions
```

## Dependencies

- `Granit`
- `Granit.DataLookup.Abstractions`

## Documentation

See the [full documentation](https://granit-fx.dev).
