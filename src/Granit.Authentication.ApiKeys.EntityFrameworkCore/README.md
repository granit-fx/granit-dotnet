# Granit.Authentication.ApiKeys.EntityFrameworkCore

EF Core persistence for `Granit.Authentication.ApiKeys`. Provides `EfCoreApiKeyStore`,
`AuthenticationApiKeysDbContext`, and entity configuration with SHA-256 indexed lookups and soft delete.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Authentication.ApiKeys.EntityFrameworkCore
```

## Dependencies

- `Granit.Authentication.ApiKeys`
- `Granit.Persistence`

## Getting started

Register the isolated `AuthenticationApiKeysDbContext` with the shared connection
options:

```csharp
services.AddGranitApiKeysEntityFrameworkCore(
    configureShared: options => options.UseNpgsql(connectionString));
```

Tables default to the host schema via `GranitApiKeysDbProperties.DbSchema` — set
this static property **before** `ConfigureServices` completes (EF Core caches the
compiled model). For schema-per-tenant isolation, pass `configureSchemaPerTenant:`.
If you fold the tables into your own tenant `DbContext` via
`modelBuilder.ConfigureApiKeysModule()`, set `GranitApiKeysDbProperties.DbSchema = null`
so the read-path `AuthenticationApiKeysDbContext` emits unqualified table names that
resolve through the tenant `search_path`.

## Documentation

See the [full documentation](https://granit-fx.dev).
