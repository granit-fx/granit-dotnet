# Granit.Analytics

Declarative Business Intelligence primitives for Granit. Pairs with `QueryDefinition`
(lists rows) and `ExportDefinition` (extracts rows): `MetricDefinition` aggregates
them into a single value (`Count`, `Sum`, `Avg`, `Min`, `Max`). Lightweight
abstractions package with no EF Core dependency — execute via
`Granit.Analytics.EntityFrameworkCore`.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Analytics
```

## Dependencies

- `Granit`
- `Granit.QueryEngine.Abstractions`

## Documentation

See the [full documentation](https://granit-fx.dev).
