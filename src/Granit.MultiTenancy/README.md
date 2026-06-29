# Granit.MultiTenancy

Multi-tenant management with tenant resolution from JWT/Header, AsyncLocal context, and ASP.NET Core middleware.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.MultiTenancy
```

## Dependencies

- `Granit`

## Integration

### 1. Register the module

Reach the module through the `[DependsOn]` graph from your root module — this
auto-wires `AddGranitMultiTenancy()`, which binds and validates
`MultiTenancyOptions` at startup (`BindConfiguration`, `ValidateDataAnnotations`,
`ValidateOnStart`):

```csharp
[DependsOn(typeof(GranitMultiTenancyModule))]
public sealed class YourHostModule : GranitModule;
```

### 2. Configure the `MultiTenancy` section

```json
{
  "MultiTenancy": {
    "IsEnabled": true,
    "TenantIdClaimType": "tenant_id",
    "TenantIdHeaderName": "X-Tenant-Id",
    "HeaderTrustMode": "CrossValidate",
    "DomainTemplate": "{0}.example.com",
    "TenantIsolation": {
      "Strategy": "SchemaPerTenant",
      "HostSchema": "host"
    }
  }
}
```

`TenantIsolation` (`Strategy`/`HostSchema`) is consumed by
`Granit.Persistence.EntityFrameworkCore` when you add a persistence companion.
`QueryStringParamName` must stay empty outside Development — the options
validator fails startup if it is set in non-Development environments, since it
would allow arbitrary tenant selection.

### 3. Wire the middleware

Call `app.UseGranitMultiTenancy()` in the pipeline **after**
`UseAuthentication` and **before** `UseAuthorization`, so `context.User` is
populated for JWT-claim resolution and the tenant context is set before
authorization and output-cache key computation:

```csharp
app.UseAuthentication();
app.UseGranitMultiTenancy();
app.UseAuthorization();
```

### Companion packages

- Persistence: `Granit.MultiTenancy.EntityFrameworkCore`.
- Endpoints (tenant CRUD): `Granit.MultiTenancy.Endpoints` (`MapGranitMultiTenancy()`).
- Runtime auto-provisioning: `Granit.MultiTenancy.Provisioning` (via `[DependsOn]`).

## Documentation

See the [full documentation](https://granit-fx.dev).
