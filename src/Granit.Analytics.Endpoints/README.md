# Granit.Analytics.Endpoints

Minimal API endpoint for `Granit.Analytics` inline metrics. Generic
`POST /metrics/{name}` route resolves a registered `MetricDefinition`, applies the
`QueryEngine` filter pipeline plus an optional period filter, executes the aggregation
through `Granit.Analytics.EntityFrameworkCore`, caches the result via FusionCache (key
composed with `tenantId` — security boundary), and supports current-vs-previous
period comparison with computed delta.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Analytics.Endpoints
```

## Dependencies

- `Granit.Analytics`
- `Granit.Analytics.EntityFrameworkCore`
- `Granit.Authorization`
- `Granit.Caching`
- `Granit.MultiTenancy`
- `Granit.Validation`

## Documentation

See the [full documentation](https://granit-fx.dev).
