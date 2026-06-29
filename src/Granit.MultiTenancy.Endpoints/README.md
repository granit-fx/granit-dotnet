# Granit.MultiTenancy.Endpoints

Minimal API endpoints for Granit multi-tenant management: tenant CRUD, activation/deactivation with permission-based access control.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.MultiTenancy.Endpoints
```

## Dependencies (from `[DependsOn]`)

- `Granit.Authorization`
- `Granit.MultiTenancy`
- `Granit.Validation`
- `Granit.Workspaces.Abstractions`

## Integration

Map the endpoints during routing setup (e.g. on your versioned API group):

```csharp
api.MapGranitMultiTenancy();
```

This exposes tenant CRUD and activation/deactivation endpoints under
`/multi-tenancy/tenants`. Tenant **listing** is intentionally not mapped here, to
keep the module decoupled from EF Core and the query engine — register a
companion `Granit.QueryEngine` endpoint at the same prefix (e.g.
`api.MapGranitQuery<Tenant>(..., "multi-tenancy/tenants", ...)`) for the list
endpoint.

## Documentation

See the [full documentation](https://granit-fx.dev).
