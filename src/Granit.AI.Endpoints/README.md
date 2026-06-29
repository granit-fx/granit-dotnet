# Granit.AI.Endpoints

Minimal API endpoints for administering Granit AI workspaces, querying usage records, and proxying chat completions and embedding generation. Protected by granular AI.* permission policies.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.AI.Endpoints
```

## Integration

Declare the module on your host module so its `ConfigureServices` runs, then map the endpoints on
your API route group:

```csharp
// In your host module:
[DependsOn(typeof(GranitAIEndpointsModule))]
public sealed class MyHostModule : GranitModule { }

// In Program.cs, on the API route group:
api.MapGranitAI();
```

Without `MapGranitAI()` the endpoints are never mapped, even with the package referenced.

## Dependencies

- `Granit.AI`
- `Granit.Authorization`
- `Granit.QueryEngine.AspNetCore`
- `Granit.Validation`

## Documentation

See the [full documentation](https://granit-fx.dev).
