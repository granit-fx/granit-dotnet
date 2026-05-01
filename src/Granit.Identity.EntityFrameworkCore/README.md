# Granit.Identity.EntityFrameworkCore

EF Core persistence for the canonical `User` aggregate shipped by
`Granit.Identity` (per [ADR-051](../../docs-site/src/content/docs/dotnet/architecture/adr/051-user-aggregate-and-parties-bridge.md)).
Wires the isolated `IdentityDbContext`, the `User` entity configuration,
and the EF Core implementation of `IUserDirectoryQueryableSource` consumed
by the QueryEngine and OData feed.

## Why

`Granit.Identity` (foundation) declares the `User` aggregate root, the
`UserQueryDefinition`, the `UserExportDefinition`, the `UserEntityDefinition`,
and the `IUserDirectoryQueryableSource` abstraction — but never references
EF Core. This companion package supplies the storage shell so consumers
can host the user table in their existing PostgreSQL deployment.

Migrations are NOT part of this package — they live in the consuming
app per the framework convention. The DbContext exposes `Database.EnsureCreated`
for tests; production hosts run `dotnet ef migrations add InitialIdentity`
against their own migrations assembly.

## Usage

```csharp
// Program.cs
builder.Services
    .AddGranitIdentityEntityFrameworkCore(opt =>
        opt.UseNpgsql(connectionString));
```

The host's existing `dotnet ef migrations add` flow picks up the new
DbContext alongside the other module DbContexts. The Showcase regenerates
its seed; no migration required for greenfield apps.

## What ships

| Class | Purpose |
| ----- | ------- |
| `IdentityDbContext` | Isolated DbContext for the `User` aggregate. Calls `ApplyGranitConventions` so tenant + soft-delete + audit filters apply automatically. |
| `EntityConfigurations.UserConfiguration` | EF Core entity configuration. Pins column lengths (Email up to 320 RFC 5321 chars, etc.), declares the tenant + email indexes. No unique index on email — multiple users may share an email per ADR-051's Odoo-style choice. |
| `Internal.EfUserDirectoryQueryableSource` | Returns `IQueryable<User>` over the `Users` set with `AsNoTracking`. Composes with the QueryEngine pipeline + the OData feed per ADR-050. |
| `Extensions.IdentityModelBuilderExtensions.ConfigureGranitIdentityModule` | `ModelBuilder` extension to fold the entity config into a host-owned DbContext if needed. |
| `Extensions.IdentityEntityFrameworkCoreServiceCollectionExtensions.AddGranitIdentityEntityFrameworkCore` | DI registration entry point. |

## Cross-references

- [ADR-051](../../docs-site/src/content/docs/dotnet/architecture/adr/051-user-aggregate-and-parties-bridge.md) — User aggregate in Granit.Identity + optional Parties bridge.
- [ADR-050](../../docs-site/src/content/docs/dotnet/architecture/adr/050-odata-edm-whitelist-via-entity-definition.md) — OData EDM whitelist (the User aggregate becomes OData-eligible because its EntityDefinition references Query + Export).
- `Granit.Identity` — declares the `User` aggregate, abstractions, and declarative companions.
