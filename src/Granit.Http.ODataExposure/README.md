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
app.MapGranitODataEndpoints("/api/{version}/odata", opts =>
{
    opts.EntitySet<Invoice, InvoiceQueryDefinition>("Invoices")
        .RequirePermission("OData.Invoicing.Invoices.Read");

    opts.EntitySet<Customer, CustomerQueryDefinition>("Customers")
        .RequirePermission("OData.Invoicing.Customers.Read");
});
```

Power BI Desktop then connects via *Get Data → OData feed* with the URL
`https://your-app/api/{version}/odata` — discovers `Invoices` and `Customers`
through the OData service document, queries them with `$filter` /
`$select` / `$top` / `$orderby`, and never sees rows belonging to other
tenants.

## Request flow

Per EntitySet `GET /api/{version}/odata/{Name}?$filter=…&$select=…&$top=…`:

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

## Strict-config validator (C6 #1395)

Every EntitySet MUST explicitly declare two security-sensitive intents:

1. **Permission** — call `.RequirePermission(...)` (gated) **OR** `.AllowAnonymousAccess()` (public-feed scenario, e.g. tenant-agnostic reference data).
2. **`$expand` policy** — call `.ExpandWhitelist(...)` (allow listed navigations) **OR** `.DisableExpand()` (no navigation exposed).

Without one of each, `MapGranitODataEndpoints` throws at host startup with a list of every misconfigured EntitySet. The framework deliberately does NOT default-deny silently — silent defaults let convention drift reach production unchecked. Failing fast at composition time is the equivalent of an architecture test for a config surface that lives inside a closure (and is therefore not statically reflectable).

## Hardening (per EntitySet)

The fluent builder ships safe-by-default caps; override per set when the
data product warrants it:

```csharp
opts.EntitySet<Invoice, InvoiceQueryDefinition>("Invoices")
    .RequirePermission("OData.Invoicing.Invoices.Read")  // strict-config: required
    .ExpandWhitelist("Customer")                         // strict-config: required (or DisableExpand())
    .MaxTop(2500)                                        // default 5000 — silently clamps user $top
    .PageSize(500)                                       // default 1000 — used when caller omits $top
    .EnableCount()                                       // default disabled — opt in for cheap-to-count tables
    .MaxExpansionDepth(2);                               // default 1 — flat expand only
```

Behaviour:

- **`$top` clamping** — values above `MaxTop` are silently capped and the
  response carries `OData-MaxTop-Applied: <cap>` so observability tooling
  can spot misconfigured BI refresh jobs.
- **`$count=true`** — returns `400 Bad Request` unless `.EnableCount()` was
  called; protects huge tables from full-table-scan counts on every
  refresh.
- **`$expand=<prop>`** — returns `400 Bad Request` if the property is not
  in `ExpandWhitelist`. An empty whitelist (or no call at all) disables
  expand entirely.
- **OTel telemetry** — every rejected query bumps
  `granit.odata.query.rejected` (tagged with `entity_set`, `reason`,
  `tenant_id`); every clamped `$top` bumps `granit.odata.query.top_clamped`.

## Rate limiting (per tenant)

Every OData route is gated by the `granit-odata` rate-limit policy from
`Granit.RateLimiting`. The host MUST configure quotas in
`appsettings.json`:

```json
{
  "RateLimiting": {
    "Policies": {
      "granit-odata": {
        "Algorithm": "SlidingWindow",
        "PartitionBy": "Tenant",
        "PermitLimit": 60,
        "Window": "00:01:00"
      }
    }
  }
}
```

Behaviour: requests are partitioned per tenant (one bucket per tenant);
once the bucket is empty, the route returns `429 Too Many Requests` with
a `Retry-After` header. Accepted requests carry `X-RateLimit-Limit` and
`X-RateLimit-Remaining` so BI tools can see how much budget is left.

Recommended defaults: 60 req/min for interactive users, 600 req/min for
service accounts (set via `Granit.Features` plan-based quotas if the
host has that integration wired). Reduce for huge datasets where each
request can trigger a long-running query.

## Limits

- v1 is read-only — no `POST` / `PATCH` / `DELETE` per EntitySet.
- v1 ships collection access (`GET /Invoices`); single-entity-by-key access
  (`GET /Invoices(<id>)`) is deferred upstream — see
  [OData/AspNetCoreOData#1567](https://github.com/OData/AspNetCoreOData/issues/1567).
- Native `Microsoft.AspNetCore.OpenApi` does not produce a complete OData
  schema (upstream [#1381](https://github.com/OData/AspNetCoreOData/issues/1381));
  the OData metadata document at `/{prefix}/$metadata` (CSDL XML) is the
  authoritative discovery surface — Power BI / Excel / Tableau consume it
  natively.
