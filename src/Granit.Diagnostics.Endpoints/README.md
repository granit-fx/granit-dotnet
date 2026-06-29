# Granit.Diagnostics.Endpoints

Admin monitoring endpoint for Granit health checks. Exposes aggregated
health status of all registered services behind a permission-gated
Minimal API endpoint.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Diagnostics.Endpoints
```

## Dependencies

- `Granit.Authorization`
- `Granit.Diagnostics`
- `Granit.Http.ApiDocumentation`
- `Granit.Validation`
- `Granit.Workspaces.Abstractions`

## Usage

Referencing the module via `[DependsOn]` registers its services, but the
monitoring endpoint is not auto-mapped. Map it on your API route group:

```csharp
using Granit.Diagnostics.Endpoints.Extensions;

api.MapGranitDiagnosticsMonitoring();
```

This exposes `GET {prefix}/diagnostics/health` (default prefix `diagnostics`,
e.g. `/api/v1/diagnostics/health` under a versioned group). The endpoint
requires authorization and is gated by `Diagnostics.Monitoring.Read`.

## Documentation

See the [full documentation](https://granit-fx.dev).
