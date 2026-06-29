# Granit.Diagnostics

Kubernetes health checks infrastructure for Granit applications. Exposes liveness, readiness and startup probes with anti-stampede cache and structured JSON format.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Diagnostics
```

## Dependencies

- `Granit.Timing`

## Usage

`GranitDiagnosticsModule` registers the health-check services automatically (it
calls `AddGranitDiagnostics()` for you), but the probe endpoints are not mapped
until you invoke `MapGranitHealthChecks()`. Call it after `app.Build()`:

```csharp
using Granit.Diagnostics.Extensions;

app.MapGranitHealthChecks();
```

This exposes the three Kubernetes probes: `/health/live` (liveness, always 200),
`/health/ready` (readiness, dependencies tagged `readiness`) and
`/health/startup` (dependencies tagged `startup`).

Register dependency checks beforehand, for example from your host module's
`ConfigureServices`:

```csharp
context.Services.AddGranitDbContextHealthCheck<MyAppDbContext>();
context.Services.AddGranitRedisHealthCheck();
```

## Documentation

See the [full documentation](https://granit-fx.dev).
