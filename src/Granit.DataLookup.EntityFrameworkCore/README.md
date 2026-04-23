# Granit.DataLookup.EntityFrameworkCore

Entity Framework Core adapter for **Granit.DataLookup**.

Provides `QueryableLookupSource<T>` — a generic lookup source that wraps an
`IQueryable<T>` with value/label selectors and an optional search predicate. Executed
via EF Core's async materialization, it honors tenant filters, soft-delete filters,
and any other conventions declared on the underlying `DbContext`.

## Registration

```csharp
services.AddQueryableLookup<Tenant, TenantsDbContext>(
    name: "tenants",
    valueSelector: t => t.Id,
    labelSelector: t => t.Name,
    searchPredicate: (t, search) => t.Name.Contains(search),
    requiredPermission: "Platform.Tenants.Read");
```

## Roadmap

PR 2 will add a higher-level `QueryDefinitionLookupSource<T>` that plugs directly into
`Granit.QueryEngine` so module authors can write:

```csharp
builder.AsLookup("tenants", value: t => t.Id, label: t => t.Name)
       .WithRequiredPermission("Platform.Tenants.Read");
```
