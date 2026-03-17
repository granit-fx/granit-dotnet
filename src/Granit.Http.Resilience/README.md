# Granit.Http.Resilience

Standardized outbound HTTP resilience for Granit modules. Wraps `Microsoft.Extensions.Http.Resilience`
(Polly v8) and exposes `AddGranitHttpClient()` with retry, circuit breaker, and per-request timeout.
Per-client settings are overridable from `appsettings.json` without redeploying.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Http.Resilience
```

## Dependencies

- `Granit.Core`
- `Microsoft.Extensions.Http.Resilience`

## Documentation

See the [full documentation](https://granit-fx.dev).
