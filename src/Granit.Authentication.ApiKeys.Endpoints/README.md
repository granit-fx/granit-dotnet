# Granit.Authentication.ApiKeys.Endpoints

Minimal API endpoints for API key management. Provides CRUD, revocation, rotation,
scope updates, and permission-based authorization for `ApiKeyEntry` entities.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Authentication.ApiKeys.Endpoints
```

## Dependencies

- `Granit.Http.ApiDocumentation`
- `Granit.Authentication.ApiKeys`
- `Granit.Authorization`
- `Granit.QueryEngine`
- `Granit.Timing`
- `Granit.Validation`

## Usage

Reference the module from your host module so its services are registered:

```csharp
[DependsOn(typeof(GranitAuthenticationApiKeysEndpointsModule))]
public sealed class MyAppModule : GranitModule { }
```

The endpoints are not auto-registered. On your API route group, call
`MapGranitApiKeys()` to map the key management endpoints (list, get, create,
revoke, rotate, update scopes):

```csharp
api.MapGranitApiKeys();
```

The endpoints are permission-gated (`AuthenticationApiKeys.Keys.Read`,
`Create`, `Revoke`, `Rotate`, `UpdateScopes`) and require authentication.
An optional `Action<ApiKeysEndpointsOptions>` lets you customize the route
prefix (default `authentication`) and OpenAPI tag (default `API Keys`).

## Documentation

See the [full documentation](https://granit-fx.dev).
