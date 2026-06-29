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
    .AddGranitODataExposure()        // wires the OData runtime + ODataQueryOptions<T> binding
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

## Surface contract — EntityDefinition required (ADR-050)

Every EntitySet MUST target an entity for which an
`EntityDefinition<TEntity>` is registered, AND the EntityDefinition MUST
reference an `ExportDefinition<TEntity>` via `b.Export<TExportDefinition>()`.
The startup validator throws otherwise — there is no silent fallback.

```csharp
public sealed class InvoiceEntityDefinition : EntityDefinition<Invoice>
{
    public override string Name => "Granit.Invoicing.Invoice";

    protected override void Configure(EntityDefinitionBuilder<Invoice> b)
    {
        b.DisplayKey("Entity:Invoice").PermissionGroup("Invoicing.Invoices");
        b.Query<InvoiceQueryDefinition>();
        b.Export<InvoiceExportDefinition>();   // ← drives the OData EDM whitelist
        // Form / Detail / Relations as usual…
    }
}
```

The OData EDM EntityType is built by:

1. Resolving the `EntityDefinition` for the target entity from
   `IEnumerable<IEntityDefinitionDescriptor>` (registered via
   `services.AddEntityDefinition<...>()`).
2. Following `EntityDefinition.Descriptor.ExportDefinitionType` to the
   matching `ExportDefinition`.
3. Whitelisting properties from `ExportDefinition.GetFields()` filtered to
   `IsNavigation == false` and `PropertyPath` containing no `.` (flat
   scalar fields only for v1; navigation paths land in a follow-up that
   derives `NavigationProperty` from `EntityDefinition.Relations`).
4. Allowing the navigation properties listed in
   `.ExpandWhitelist(...)` on the EntitySet builder (existing mechanism).

Properties not in the whitelist are removed from the EDM via
`EntityTypeConfiguration.RemoveProperty(...)` BEFORE convention discovery
runs — notably `AggregateRoot.DomainEvents` /
`AggregateRoot.IntegrationEvents`, which would otherwise leak into
`$metadata` because they are publicly exposed on every aggregate root to
satisfy `IDomainEventSource` / `IIntegrationEventSource`.

> **Why EntityDefinition rather than Export directly?** EntityDefinition
> is the orchestrator (Phase 1 — `Granit.Entities`). Pinning OData to it
> aligns the BI surface with the framework's entity-modeling direction:
> as more modules ship `EntityDefinition`s in subsequent phases, their
> entities become OData-exposable. See [ADR-050](../../docs-site/src/content/docs/dotnet/architecture/adr/050-odata-edm-whitelist-via-entity-definition.md)
> for the full trade-off analysis.

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

## Host-feed (cross-tenant BI for host operators)

`MapGranitODataEndpoints` (above) covers the standard tenant-scoped feed: a
tenant analyst sees only their tenant's rows. For legitimate cross-tenant
analytics — finance ops (MRR/ARR across all tenants), compliance (audit
entries cross-tenant), capacity planning — use the dedicated host-feed mount.

```csharp
app.MapGranitODataHostEndpoints("/api/{version}/odata/host", opts =>
{
    opts.EntitySet<Tenant, TenantQueryDefinition>("Tenants")
        .RequirePermission("OData.Host.Platform.Tenants.Read")  // MUST be MultiTenancySides.Host
        .DisableExpand();

    opts.EntitySet<Invoice, InvoiceQueryDefinition>("InvoicesAllTenants")
        .RequirePermission("OData.Host.Invoicing.Invoices.Read")
        .AcknowledgeCrossTenantExposure(q =>
            q.IgnoreQueryFilters([GranitFilterNames.MultiTenant]))    // explicit per-query bypass
        .ExpandWhitelist("Customer");
});
```

Three strict-config gates are added on top of the tenant-feed validator:

1. **Permission must be `MultiTenancySides.Host`.** The validator resolves
   the permission name through `IPermissionDefinitionManager` at startup
   and refuses `Tenant` or `Both`. A permission without an `IPermissionDefinitionProvider`
   declaration is also refused.
2. **No anonymous access.** The host builder does not expose
   `AllowAnonymousAccess()` — every host-feed set is gated.
3. **`AcknowledgeCrossTenantExposure(...)` mandatory for `IMultiTenant` entities.**
   The host writes the per-query bypass lambda at the call site. Without it,
   the framework filter `tenantId == currentTenant.Id` returns no rows for a
   tenantless caller — fail-closed. The bypass lambda is `q => q.IgnoreQueryFilters([GranitFilterNames.MultiTenant])`
   on EF Core; alternative providers can plug their own.

Other distinctions:

- **Distinct OData container name.** Tenant-feed uses `Container`; host-feed uses
  `HostContainer`. A BI client that mixes the two `$metadata` documents sees an
  immediate schema mismatch.
- **Distinct rate-limit policy.** `granit-odata-host` (recommended `PartitionBy: User`,
  wider quotas — fewer users, heavier queries) vs `granit-odata` (recommended
  `PartitionBy: Tenant`).
- **Telemetry tag `feed_kind=host|tenant`** on every `ODataExposureMetrics`
  counter so audit dashboards can filter cross-tenant access events distinctly
  (ISO 27001 A.12.4).

### When to use Host-feed vs Granit.Analytics

| Need | Use |
| ---- | --- |
| Cross-tenant aggregate KPI for a host dashboard | `Granit.Analytics` (`MetricDefinition`) |
| Cross-tenant tabular data for Power BI / Excel / Tableau | Host-feed (this module) |
| Per-tenant tabular data for tenant admins | Tenant-feed (this module) |
| Per-tenant aggregate KPI inside an admin grid | `Granit.Analytics` |

`Granit.Analytics` is the right fit when the consumer is the application's own
admin UI and the value is one or a few aggregated numbers per call. Host-feed
is the right fit when the consumer is an external BI tool that needs full
tabular data and benefits from `$filter` / `$select` / `$top` composition.
