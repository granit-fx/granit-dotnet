# Granit.Http.ODataExposure

Bridges registered `QueryDefinition<TEntity>` instances to OData v4 EntitySets
so external BI tools (Power BI, Excel, Tableau, Qlik) can consume the
application's data via the standard "Get Data → OData feed" connector. No
custom connector, no read-replica SQL access, no bypass of multi-tenancy.

## Why

Granting BI tools direct read-only SQL access to a multi-tenant production
database is a common industry pattern and a common cause of cross-tenant
leaks: one missing `WHERE tenant_id = …` in a custom view and a tenant reads
another tenant's data. This package is the only sanctioned bridge for the
Granit framework — every OData request flows through the same filter
pipeline as the application's own grid endpoints, inheriting tenant
filtering, soft-delete, and per-EntitySet permissions.

## Usage

```csharp
// Program.cs
builder.Services
    .AddGranitOData()                // wires the OData runtime + ODataQueryOptions<T> binding
    .AddGranitQueryEngine()          // host's existing QueryEngine registration
    .AddQueryDefinition<Invoice, InvoiceQueryDefinition>();

// after authentication + authorization middleware
app.MapGranitODataEndpoints("/api/granit/odata", opts =>
{
    opts.EntitySet<Invoice, InvoiceQueryDefinition>("Invoices")
        .RequirePermission("OData.Invoicing.Invoices.Read");

    opts.EntitySet<Customer, CustomerQueryDefinition>("Customers")
        .RequirePermission("OData.Invoicing.Customers.Read");
});
```

Power BI Desktop then connects via *Get Data → OData feed* with the URL
`https://your-app/api/granit/odata` — discovers `Invoices` and `Customers`
through the OData service document, queries them with `$filter` /
`$select` / `$top` / `$orderby`, and never sees rows belonging to other
tenants.

## Request flow

Per EntitySet `GET /api/granit/odata/{Name}?$filter=…&$select=…&$top=…`:

1. **Authentication** — enforced by the surrounding pipeline (the framework's
   bearer token / DPoP setup).
2. **Permission gate** — when the EntitySet declared one via
   `RequirePermission(...)`, the route returns `403` if the user lacks it.
3. **`IQueryableSource<TEntity>`** is resolved from DI — emits an
   `IQueryable<TEntity>` already filtered by the host's
   `ApplyGranitConventions` (tenant + soft-delete).
4. **`IQueryEngine.BuildFilteredQuery(source, new QueryRequest())`** layers
   the `QueryDefinition`'s filter pipeline (presets, quick filters, global
   search). User filters compose ON TOP of these — never instead of them.
5. **`ODataQueryOptions<TEntity>.ApplyTo(filtered)`** layers the user's
   `$filter` / `$select` / `$top` / `$skip` / `$orderby` on top.

The order is load-bearing: tenant first, framework filters second, user
query third.

## Limits

- v1 is read-only — no `POST` / `PATCH` / `DELETE` per EntitySet.
- v1 ships collection access (`GET /Invoices`); single-entity-by-key access
  (`GET /Invoices(<id>)`) is deferred upstream — see
  [OData/AspNetCoreOData#1567](https://github.com/OData/AspNetCoreOData/issues/1567).
- `$expand` whitelist + `$top` / `$count` throttling + rate-limiting are
  follow-up scope (story C3 / #1392).
- Native `Microsoft.AspNetCore.OpenApi` does not produce a complete OData
  schema (upstream [#1381](https://github.com/OData/AspNetCoreOData/issues/1381));
  the OData metadata document at `/{prefix}/$metadata` (CSDL XML) is the
  authoritative discovery surface — Power BI / Excel / Tableau consume it
  natively.
