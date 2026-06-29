# Granit.Observability

Serilog and OpenTelemetry (OTLP to Loki/Tempo/Mimir) for Granit applications.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Observability
```

## Dependencies

- `Granit`

## Integration

The module auto-wires when pulled into the host module graph — via the Essentials
bundle, or explicitly with `[DependsOn(typeof(GranitObservabilityModule))]`. Its
`ConfigureServices` calls `AddGranitObservability()` for you; no manual call is
required.

When the host already registers OpenTelemetry's cross-cutting `UseOtlpExporter()`
(e.g. .NET Aspire `AddServiceDefaults()`), Granit detects it and **defers** OTLP
wiring to that registration — call `AddServiceDefaults()` / `UseOtlpExporter()`
before `AddGranitObservability()`.

## Configuration

All settings live under the optional `Observability` section
(`ObservabilityOptions`). Smart fallbacks make the section omittable:

| Key | Default | Fallback |
| --- | --- | --- |
| `ServiceName` | `unknown-service` | host `ApplicationName` |
| `ServiceVersion` | `0.0.0` | — |
| `OtlpEndpoint` | `http://localhost:4317` | `OTEL_EXPORTER_OTLP_ENDPOINT` env var |
| `ServiceNamespace` | `my-company` | — |
| `Environment` | `development` | host `EnvironmentName` |
| `EnableTracing` | `true` | — |
| `EnableMetrics` | `true` | — |

```json
{
  "Observability": {
    "ServiceName": "my-backend",
    "OtlpEndpoint": "http://otel-collector:4317"
  }
}
```

## Documentation

See the [full documentation](https://granit-fx.dev).
