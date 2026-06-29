# Granit.Http.RateLimiting

ASP.NET Core binding for [`Granit.RateLimiting`](https://granit-fx.dev). Applies the framework-pure
rate limiting core to HTTP endpoints: a per-endpoint filter emitting `429 Too Many Requests` +
`Retry-After` and `X-RateLimit-*` headers, plus RFC 7807 mapping of `RateLimitExceededException` to
HTTP 429.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Http.RateLimiting
```

## Usage

Reference `GranitHttpRateLimitingModule` — it pulls in the core `GranitRateLimitingModule`
automatically:

```csharp
[DependsOn(typeof(GranitHttpRateLimitingModule))]
public sealed class AppHostModule : GranitModule { }
```

Then guard endpoints with the filter:

```csharp
group.MapPost("/uploads", UploadAsync)
    .RequireGranitRateLimiting("uploads"); // policy under RateLimiting:Policies:uploads
```

Policies are configured under `RateLimiting:Policies` (see `Granit.RateLimiting`):

```json
{
  "RateLimiting": {
    "Enabled": true,
    "KeyPrefix": "rl",
    "Policies": {
      "uploads": {
        "Algorithm": "SlidingWindow",
        "PermitLimit": 100,
        "Window": "00:01:00",
        "PartitionBy": "Tenant"
      }
    }
  }
}
```

## Dependencies

- `Granit.RateLimiting`
- `Granit.Http.ExceptionHandling`

## Documentation

See the [full documentation](https://granit-fx.dev).
