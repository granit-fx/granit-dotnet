# Granit.Analytics.EntityFrameworkCore

EF Core executor for `Granit.Analytics` `MetricDefinition`. Runs `Count` / `Sum` /
`Avg` / `Min` / `Max` aggregations through the `Granit.QueryEngine` filter pipeline
so KPIs always match grid row counts for the same filter spec, and locks the
empty-set semantics (Sum / Count → 0, Avg / Min / Max → null) that the Analytics
module promises.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Analytics.EntityFrameworkCore
```

## Dependencies

- `Granit.Analytics`
- `Granit.Persistence`
- `Granit.QueryEngine.EntityFrameworkCore`

## Documentation

See the [full documentation](https://granit-fx.dev).
