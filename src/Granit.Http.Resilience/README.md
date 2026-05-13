# Granit.Http.Resilience

Standardized outbound HTTP resilience for Granit modules. Wraps `Microsoft.Extensions.Http.Resilience`
(Polly v8) and exposes `AddGranitHttpClient()` with retry, circuit breaker, and per-request timeout.
Per-client settings are overridable from `appsettings.json` without redeploying.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Http.Resilience
```

## Usage

```csharp
services.AddGranitHttpClient("catalog-service",
    client => client.BaseAddress = new Uri("https://catalog-api"));
```

The standard pipeline applies: retry (3 attempts, exponential back-off), circuit breaker,
and per-request timeout. Override per-client via `appsettings.json`:

```json
{
  "HttpResilience": {
    "catalog-service": {
      "Retry": { "MaxRetryAttempts": 5 },
      "TotalRequestTimeout": { "Timeout": "00:01:00" }
    }
  }
}
```

## Telemetry

Retry, circuit-breaker, and timeout events are emitted by the upstream `Polly` meter
shipped with `Microsoft.Extensions.Http.Resilience`. Enable it in OpenTelemetry by
adding the meter name `"Polly"` to your `MeterProviderBuilder`.

## Inter-service authorization

For forwarding caller identity to downstream services, do **not** propagate the inbound
`Authorization` header (confused-deputy vulnerability). Use
`AddOnBehalfOfHttpClient` from `Granit.Oidc.TokenManagement`, which performs
[RFC 8693](https://datatracker.ietf.org/doc/html/rfc8693) token exchange to a narrowed
audience and optionally binds the exchanged token with
[DPoP](https://datatracker.ietf.org/doc/html/rfc9449).

## Dependencies

- `Granit`
- `Microsoft.Extensions.Http.Resilience`

## Documentation

See the [full documentation](https://granit-fx.dev).
