# Granit.DataExchange.Json

JSON export support for `Granit.DataExchange`.

**Export**: `JsonExportWriter` implementing `IExportWriter` (format `"json"`) with
`ExportFormatCapabilities.Structured` — so it serializes hierarchical/complex fields
(`ComplexField<TValue>`) in full, enabling round-trippable structured exports. Per-row
streaming flush; cycle-safe serialization (`ReferenceHandler.IgnoreCycles`).

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.DataExchange.Json
```

## Dependencies

- `Granit.DataExchange`

## Documentation

See the [full documentation](https://granit-fx.dev).
