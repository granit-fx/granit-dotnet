# Granit.Authorization.Endpoints

Minimal API endpoints for RBAC permission management. Exposes current-user
permissions (`GET /me`), permission definitions (`GET /definitions`), and
admin grant/revoke routes (`GET/PUT/DELETE /roles/{roleName}/...`).

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Authorization.Endpoints
```

## Dependencies

- `Granit.Authorization`

## Usage

Reference the module from your host module so its services are registered:

```csharp
[DependsOn(typeof(GranitAuthorizationEndpointsModule))]
public sealed class MyAppModule : GranitModule { }
```

Then expose the endpoints by calling `MapGranitAuthorization()` (or
`api.MapGranitAuthorization()` inside a route group):

```csharp
api.MapGranitAuthorization();
```

## Documentation

See the [full documentation](https://granit-fx.dev).
