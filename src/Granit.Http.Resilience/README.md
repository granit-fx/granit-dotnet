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

### Resilient HTTP client

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

### Auth token propagation

For microservice architectures where the incoming JWT token must be forwarded to
downstream services:

```csharp
services.AddGranitHttpClient("catalog-service")
    .AddAuthTokenPropagation();
```

The handler reads the `Authorization` header from the current `HttpContext` and sets it
on every outgoing request. If the outgoing request already has an `Authorization` header,
it is left untouched (explicit overrides are respected).

## Dependencies

- `Granit`
- `Microsoft.Extensions.Http.Resilience`

## Documentation

See the [full documentation](https://granit-fx.dev).
