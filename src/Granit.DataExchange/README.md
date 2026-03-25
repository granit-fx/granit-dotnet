# Granit.DataExchange

Core data exchange infrastructure for the Granit framework.

**Import**: mini-ETL pipeline Extract → Map → Validate → Execute, with a 4-tier
smart mapping suggestion engine (Saved → Exact → Fuzzy → Semantic AI).

**Export**: tabular export (Excel/CSV) with fluent `ExportDefinition<T>`, presets,
background jobs, and roundtrip support (export → modify → reimport).

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.DataExchange
```

## Dependencies

- `Granit.Guids`
- `Granit.QueryEngine`
- `Granit.Timing`
- `Granit.Validation`

## Documentation

See the [full documentation](https://granit-fx.dev).
