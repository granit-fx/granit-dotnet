# Granit.DataLookup.EntityFrameworkCore

Entity Framework Core adapter for **Granit.DataLookup**.

Provides `QueryableLookupSource<T>` — a generic lookup source that wraps an
`IQueryable<T>` with value/label selectors and an optional search predicate. Executed
via EF Core's async materialization, it honors tenant filters, soft-delete filters,
and any other conventions declared on the underlying `DbContext`.

## Registration

`AddQueryableLookup` and `AddQueryDefinitionLookup` live in the
`Granit.DataLookup.EntityFrameworkCore.Extensions` namespace (not the
conventional `Microsoft.Extensions.DependencyInjection`), so the `using` is
mandatory for compilation:

```csharp
using Granit.DataLookup.EntityFrameworkCore.Extensions;

services.AddQueryableLookup<Tenant, TenantsDbContext>(
    name: "tenants",
    valueSelector: t => t.Id,
    labelSelector: t => t.Name,
    searchPredicate: (t, search) => t.Name.Contains(search),
    requiredPermission: "Platform.Tenants.Read");
```

## QueryDefinition-backed lookups

`QueryDefinitionLookupSource<T>` plugs directly into `Granit.QueryEngine`, reusing a
`QueryDefinition<T>`'s global search, sort, filters, and keyset/cursor pagination. Declare the
lookup on the definition and register the source against its `DbContext`:

```csharp
// In the QueryDefinition (base module):
protected override void Configure(QueryDefinitionBuilder<Tenant> builder) =>
    builder
        .Column(t => t.Name, c => c.Sortable().Filterable())
        .GlobalSearch(t => t.Name)
        .DefaultSort("Name")
        .SupportsCursorPagination(t => t.Id)   // enables infinite-scroll (NextCursor)
        .AsLookup("tenants", value: t => t.Id, label: t => t.Name,
            requiredPermission: "Platform.Tenants.Read");

// In the host/EF module:
services.AddQueryDefinitionLookup<Tenant, TenantsDbContext>();
```

`ContinuationToken` round-trips to the engine's keyset cursor, so paging through
`GET /lookups/tenants?continuationToken=…` yields a true infinite-scroll feed. Scope keys map
to equality filters on the matching filterable columns (cascading pickers).
