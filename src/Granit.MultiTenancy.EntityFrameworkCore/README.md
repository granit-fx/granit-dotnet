# Granit.MultiTenancy.EntityFrameworkCore

EF Core persistence for Granit.MultiTenancy. Provides Tenant aggregate root, MultiTenancyDbContext, and EfCoreTenantStore with ISO 27001 audit trail.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.MultiTenancy.EntityFrameworkCore
```

## Dependencies (from `[DependsOn]`)

- `Granit.DataLookup.EntityFrameworkCore`
- `Granit.Events`
- `Granit.MultiTenancy`
- `Granit.Persistence.EntityFrameworkCore`
- `Granit.Persistence.EntityFrameworkCore.Migrations`
- `Granit.QueryEngine.Abstractions`

## Integration

In your module's `ConfigureServices`, call the EF Core registration —
this is **required** to replace the no-op `ITenantReader` with
`EfCoreTenantStore` and register `MultiTenancyDbContext`. The module pulls in
`GranitMultiTenancyModule` via `[DependsOn]` but does **not** auto-invoke this
extension:

```csharp
[DependsOn(typeof(GranitMultiTenancyEntityFrameworkCoreModule))]
public sealed class YourHostModule : GranitModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Builder.AddGranitMultiTenancyEntityFrameworkCore(options =>
            options.UseNpgsql(connectionString));
    }
}
```

## Documentation

See the [full documentation](https://granit-fx.dev).
