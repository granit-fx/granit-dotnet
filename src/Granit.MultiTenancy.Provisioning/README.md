# Granit.MultiTenancy.Provisioning

Wolverine integration for Granit.MultiTenancy. Automatic tenant provisioning handler that runs EF Core migrations and data seeding for newly created tenants at runtime.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.MultiTenancy.Provisioning
```

## Dependencies (from `[DependsOn]`)

- `Granit.MultiTenancy`
- `Granit.Persistence.EntityFrameworkCore.Hosting`
- `Granit.Wolverine`

## Integration

Add the module to your host's `[DependsOn]` graph. No `ConfigureServices` call
is required — `TenantProvisioningHandler` is auto-discovered by Wolverine
assembly scanning and reacts to `TenantCreatedEto` to run EF Core migrations and
seeding for runtime tenant creation:

```csharp
[DependsOn(typeof(GranitMultiTenancyProvisioningModule))]
public sealed class YourHostModule : GranitModule;
```

> During `--migrate` mode Wolverine is not started and provisioning is handled
> by `GranitMigrationRunner` instead, so this module only activates for
> admin-API runtime tenant creation.

## Documentation

See the [full documentation](https://granit-fx.dev).
