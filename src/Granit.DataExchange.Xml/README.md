# Granit.DataExchange.Xml

XML export support for `Granit.DataExchange`.

**Export**: `XmlExportWriter` implementing `IExportWriter` (format `"xml"`) with
`ExportFormatCapabilities.Structured` — so it serializes hierarchical/complex fields
(`ComplexField<TValue>`) in full, enabling round-trippable structured exports. Per-row
streaming flush; cached per-type serializers; loud failure on unsupported types.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.DataExchange.Xml
```

## Dependencies

- `Granit.DataExchange`

## Documentation

See the [full documentation](https://granit-fx.dev).
