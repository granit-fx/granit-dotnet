# Granit.Hostnames.EntityFrameworkCore

EF Core persistence layer for [`Granit.Hostnames`](../Granit.Hostnames/README.md).

## What this package provides

| Type | Role |
| ---- | ---- |
| `HostnamesDbContext` | Isolated `GranitDbContext` — owns `ManagedHostname`, applies the multi-tenant query filter |
| `EfManagedHostnameStore` | `IManagedHostnameReader` + `IManagedHostnameWriter` backed by `HostnamesDbContext` |
| `EfHostnameResolver` | `IHostnameResolver` — bypasses the tenant filter (host precedes tenant context) |
| `HostnamesModelBuilderExtensions` | `ConfigureHostnamesModule()` for host-owned DbContexts |
| `GranitHostnamesDbProperties` | `DbTablePrefix` / `DbSchema` overrides |

## Table

`hostname_managed_hostnames` (prefix + schema configurable via `GranitHostnamesDbProperties`).

| Column | Type | Notes |
| ------ | ---- | ----- |
| `Id` | `uuid` | Primary key |
| `Host` | `varchar(253)` | **Globally unique** — anti-hijacking constraint |
| `OwnerType` | `varchar(100)` | Opaque owner discriminator (e.g. `"cms.site"`) |
| `OwnerId` | `uuid` | Owning resource id |
| `TenantId` | `uuid?` | Null for host-level (global) hostnames |
| `IsPrimary` | `bool` | Canonical hostname flag |
| `Status` | `varchar(20)` | Enum as string: `Pending`, `Verifying`, `Active`, `Error` |
| `ConcurrencyStamp` | `varchar(40)` | Optimistic concurrency (ADR-061) |
| Audit columns | — | `CreatedAt`, `CreatedBy`, `LastModifiedAt`, `LastModifiedBy` |

## Registration

```csharp
builder.AddGranitHostnamesEntityFrameworkCore(opt =>
    opt.UseNpgsql(connectionString));
```

The base `GranitHostnamesModule` is pulled in automatically — `GranitHostnamesEntityFrameworkCoreModule` declares
`[DependsOn(typeof(GranitHostnamesModule), ...)]` — so no separate `AddGranitHostnames()` call exists or is needed.

## Host-owned DbContext

If your application merges tables into a single DbContext:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);
    modelBuilder.ConfigureHostnamesModule();
}
```

## Migrations

This package ships no migrations — the consuming application owns them.
