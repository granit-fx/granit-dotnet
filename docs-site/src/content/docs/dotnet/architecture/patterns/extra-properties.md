---
title: "Extra Properties Pattern — Schema-Free Entity Extensibility"
description: "JSON property bag on EF Core entities with optional SQL column promotion for indexing. Enables application-level extensibility without framework schema changes."
sidebar:
  label: Extra Properties
  order: 59
---

## Definition

The Extra Properties pattern adds a schema-free JSON property bag to any EF Core
entity, enabling applications to store custom key-value pairs without modifying
the framework's database schema. Properties that need SQL indexing or query
filtering can be **promoted** to real SQL columns at startup.

Also known as: **Property Bag**, **Object Extension** (ABP), **EAV** (legacy),
**user_metadata** (Auth0), **Custom Attributes** (Keycloak).

## Diagram

```mermaid
flowchart LR
    subgraph Entity["Entity (e.g., GranitUser)"]
        JSON["ExtraPropertiesJson<br/>(JSONB column)"]
        COL1["JobTitle<br/>(SQL column)"]
        COL2["IsVip<br/>(SQL column)"]
    end

    APP["Application code"] -->|SetExtraProperty| JSON
    APP -->|MapProperty&lt;T&gt;| COL1
    APP -->|MapProperty&lt;T&gt;| COL2

    subgraph Read["Read path"]
        R1["entity.GetExtraProperty('key')"] --> JSON
        R2["EF.Property&lt;bool&gt;(u, 'IsVip')"] --> COL2
    end

    subgraph Save["Save (Interceptor)"]
        S1["Mapped values → SQL columns"]
        S2["Non-mapped values → JSON"]
        S3["No duplication"]
    end
```

## Interface

```csharp
// Granit.Domain
public interface IHasExtraProperties
{
    string? ExtraPropertiesJson { get; set; }
}
```

Any entity implementing `IHasExtraProperties` gets automatic JSON-backed
extensibility. The framework provides typed extension methods:

```csharp
// Read
string? dept = entity.GetExtraProperty("Department");
bool? isVip = entity.GetExtraProperty<bool>("IsVip");
bool exists = entity.HasExtraProperty("LicenseNumber");

// Write
entity.SetExtraProperty("Department", "Engineering");
entity.SetExtraProperty("LicenseNumber", null); // removes
```

## Granit implementation

### Level 1 — JSON bag (zero config)

Every `IHasExtraProperties` entity stores properties in a `jsonb` column
(PostgreSQL) or `nvarchar(max)` (SQL Server). No migrations needed — the column
is always present.

```csharp
// Any entity
public class ReferenceDataEntity : AuditedEntity, IHasExtraProperties
{
    public string? ExtraPropertiesJson { get; set; }
}

// Usage
country.SetExtraProperty("Alpha3Code", "BEL");
country.SetExtraProperty("IsEuMember", "true");
```

### Level 2 — SQL column promotion (for indexing)

Properties that need SQL `WHERE` clauses, indexes, or `ORDER BY` can be promoted
to real EF Core Shadow Properties at startup:

```csharp
// In the host application's module (ConfigureServices)
services.AddExtraPropertyMappings<GranitUser>(options =>
{
    options.MapProperty<string>("JobTitle", maxLength: 128);
    options.MapProperty<string>("Department", maxLength: 64);
    options.MapProperty<bool>("IsVip");
});
```

This adds real SQL columns on the entity's table. The application must then
regenerate EF Core migrations:

```bash
dotnet ef migrations add AddUserExtensions --context OpenIddictDbContext
```

### Level 3 — Sync interceptor (no duplication)

The `ExtraPropertySyncInterceptor` in `Granit.Persistence` ensures that
promoted properties are **excluded** from the JSON bag at save time:

- **Mapped property set via JSON** → interceptor moves value to SQL column,
  removes from JSON
- **Mapped property set via Shadow Property** → stays in SQL column, not in JSON
- **Non-mapped property** → stays in JSON only

This prevents data duplication (source of truth = SQL column for promoted props,
JSON for the rest).

## Entities using this pattern

| Entity | Package | JSON column |
|--------|---------|-------------|
| `GranitUser` | `Granit.OpenIddict` | `CustomAttributesJson` |
| `UserCacheEntry` | `Granit.Identity.Federated.EntityFrameworkCore` | `ExtraPropertiesJson` |
| `ReferenceDataEntity` | `Granit.ReferenceData.EntityFrameworkCore` | `ExtraPropertiesJson` |

## When to use each strategy

| Need | Strategy | Example |
|------|----------|---------|
| Simple key-value, no SQL queries | JSON bag (Level 1) | User preferences, UI settings |
| SQL `WHERE`, `ORDER BY`, indexes | Promoted column (Level 2) | `IsVip`, `Department` filter |
| Complex domain data, relationships | Companion Entity | `Employee` with `Salary`, `HireDate` |

## Comparison with other frameworks

| Framework | Mechanism | SQL promotion | Schema-free |
|-----------|-----------|:---:|:---:|
| **Granit** | `IHasExtraProperties` + `MapProperty<T>` | Yes | Yes |
| **ABP** | `ExtraProperties` + `ObjectExtensionManager` | Yes | Yes |
| **Auth0** | `user_metadata` / `app_metadata` | No | Yes |
| **Keycloak** | `attributes` dictionary | No | Yes |
| **Entra ID** | `extensionAttribute1-15` | Yes (fixed) | No (15 max) |
| **EAV** (Magento) | Attribute-Value pivot tables | Yes | Yes |

## Key files

| File | Purpose |
|------|---------|
| `src/Granit/Domain/IHasExtraProperties.cs` | Interface |
| `src/Granit/Domain/ExtraPropertyExtensions.cs` | Get/Set/Has extension methods |
| `src/Granit.Persistence/ExtraProperties/ExtraPropertyMappingOptions.cs` | `MapProperty<T>()` config |
| `src/Granit.Persistence/ExtraProperties/ExtraPropertySyncInterceptor.cs` | Save-time sync (no duplication) |
| `src/Granit.Persistence/ExtraProperties/ExtraPropertyModelBuilderExtensions.cs` | Applies Shadow Properties to ModelBuilder |
| `src/Granit.Persistence/ExtraProperties/ExtraPropertyServiceCollectionExtensions.cs` | DI registration helpers |

## Anti-patterns

- **Don't store structured data in ExtraProperties** — use a Companion Entity for
  complex types with relationships
- **Don't query JSON with EF Core** — promote to SQL column if you need `WHERE`
- **Don't use ExtraProperties for security-sensitive data** — the JSON column is not
  encrypted (use `IStringEncryptionService` separately)
- **Don't duplicate** — if a property is promoted to SQL, it must NOT also be in JSON
  (the interceptor enforces this)
