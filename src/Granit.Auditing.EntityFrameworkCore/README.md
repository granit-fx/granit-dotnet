# Granit.Auditing.EntityFrameworkCore

EF Core persistence for the Granit audit trail module.
Provides an isolated `AuditingDbContext` with hierarchical entity model,
change tracking interceptor, async/strict persistence modes,
`IAuditingReader` implementation, and category-based retention cleanup.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Auditing.EntityFrameworkCore
```

## Configuration

After registering the isolated `AuditingDbContext` via
`builder.AddGranitAuditingEntityFrameworkCore(...)`, the host application's DbContext must wire the
change-tracking interceptor:

```csharp
services.AddDbContextFactory<MyAppDbContext>((sp, options) =>
{
    options.UseNpgsql(connectionString);
    options.UseGranitInterceptors(sp);          // Framework interceptors first
    options.UseGranitAuditingInterceptor(sp);   // Auditing interceptor after
}, ServiceLifetime.Scoped);
```

`UseGranitAuditingInterceptor(sp)` must come **after** `UseGranitInterceptors(sp)` so audit fields
and soft-delete state are already applied when the audit interceptor reads them. Without this step,
entity changes are not captured by the audit trail.

## Dependencies

- `Granit.Auditing`
- `Granit.Persistence`

## Documentation

See the [full documentation](https://granit-fx.dev).
