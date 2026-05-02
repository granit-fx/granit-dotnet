# Granit.Entities.Customization.EntityFrameworkCore

EF Core companion for `Granit.Entities.Customization` ([ADR-053][adr-053]).
Ships an isolated `CustomizationDbContext`, the `EntityCustomization` table
configuration with a unique `(TenantId, EntityName, LayoutKind)` index, the
JSON-column conversion for the closed delta vocabulary, and the EF
implementations of `IEntityCustomizationReader` / `IEntityCustomizationWriter`
that replace the default `Null*` reader from the abstractions package.

## What's inside

| Type | Role |
| ---- | ---- |
| `CustomizationDbContext` | Isolated DbContext, follows the Granit `ApplyGranitConventions` pattern (tenant filter, soft-delete, audit interceptors) |
| `EntityCustomizationConfiguration` | Table mapping + unique index + JSON value-converter for `Deltas` |
| `EfEntityCustomizationReader` / `EfEntityCustomizationWriter` | Repository impls scoped to the request's tenant via the standard query filter |
| `AddGranitEntitiesCustomizationEntityFrameworkCore(opts => opts.UseNpgsql(...))` | Host wiring — registers DbContext + reader/writer impls (Replace overrides Null reader) |
| `ConfigureEntitiesCustomizationModule(modelBuilder)` | `ModelBuilder` extension for hosts that prefer a single shared DbContext |

## JSON column

The `Deltas` list is serialized via `System.Text.Json` (polymorphism wired in
`LayoutDelta` via `[JsonPolymorphic]` + `[JsonDerivedType]` for the three
record types). The framework keeps the column type **portable** — default
text on PostgreSQL, TEXT on SQLite, nvarchar(max) on SQL Server.

Postgres-hosted apps that want JSONB query support
(`WHERE deltas @> '[{"fieldName": "X"}]'::jsonb` for the field-inspector dev
tooling) can:

```sql
ALTER TABLE entities_customization_entity_customizations
  ALTER COLUMN deltas TYPE jsonb USING deltas::jsonb;
```

…in their first migration. No framework migrations ship per the Granit
convention.

## Composition example

```csharp
// In the host's Program.cs
builder.AddGranitEntitiesCustomization()                           // Null reader by default
       .AddGranitEntitiesCustomizationEntityFrameworkCore(opts =>  // EF reader replaces it
            opts.UseNpgsql(connectionString));
```

## See also

- [ADR-053 — Layer 1 customization model][adr-053]
- `Granit.Entities.Customization` — abstractions package
- `Granit.Entities.Customization.Endpoints` (B3) — `PUT` + audit trail
- `Granit.Entities.Endpoints` manifest composer (B4) — apply pipeline + provenance

[adr-053]: https://github.com/granit-fx/granit-dotnet/blob/develop/docs-site/src/content/docs/dotnet/architecture/adr/053-entities-customization-layer-1.md
