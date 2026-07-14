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
    opts.RequireMetadataPermission("OData.Invoicing.Metadata.Read");  // or AllowAnonymousMetadata()

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
   scalar fields — navigations are modelled as `NavigationProperty`, not
   flattened columns).
4. Allowing the navigation paths listed in `.ExpandWhitelist(...)` on the
   EntitySet builder — and, per #3005, registering every navigation-TARGET
   type reachable through a whitelisted path with the SAME export-derived
   scalar whitelist (transitive closure). A target type without a
   registered `ExportDefinition` is a startup error: it would otherwise
   enter the EDM through convention discovery with every public property
   exposed.

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
4. **`$filter` translation** — the OData filter AST is translated into the
   QueryEngine's strict `QueryPredicate` tree. Untranslatable constructs
   return `400 Bad Request` (see [`$filter` support](#filter-support) below).
5. **`IQueryEngine.BuildFilteredQuery(source, new QueryRequest(), predicate)`**
   layers the `QueryDefinition`'s filter pipeline (presets, quick filters,
   global search) AND the translated user predicate through the engine's
   single enforcement point — filterable-column whitelist, operator
   inference, structural guards. Violations return `400` listing every
   offending field.
6. **Per-route validation** — `ODataQueryOptions.Validate` runs against
   settings derived from the descriptor and the `QueryDefinition`:
   `$orderby` is whitelisted from the definition's `Sortable()` columns
   (no sortable columns → `$orderby` rejected entirely).
7. **`ODataQueryOptions<TEntity>.ApplyTo(filtered, settings, AllowedQueryOptions.Filter)`**
   layers `$select` / `$top` / `$skip` / `$orderby` on top. `$filter` is
   passed as the *ignore* flag — it was already enforced by the engine and
   is never applied twice.

The order is load-bearing: tenant first, framework filters second, user
query third — and the user's `$filter` goes through the engine, not around
it.

## `$filter` support

Since #3004 the user's `$filter` is no longer composed as arbitrary LINQ by
`ApplyTo` — it is translated into the QueryEngine predicate tree so the
`QueryDefinition`'s `Filterable()` whitelist and operator inference are
enforced once, engine-side.

Supported constructs:

| OData construct | QueryEngine translation |
| --------------- | ----------------------- |
| `and` / `or` / `not` | `And` / `Or` / `Not` predicate composition |
| `eq` / `ne` | `Eq` / `Ne` leaf |
| `X eq null` / `X ne null` | `IsNull` / `IsNotNull` null check |
| `gt` / `ge` / `lt` / `le` | `Gt` / `Gte` / `Lt` / `Lte` (mirrored when the literal is on the left) |
| `contains` / `startswith` / `endswith` | `Contains` / `StartsWith` / `EndsWith` over an entity-local string column |
| `X in ('a','b')` | `In` leaf (comma-joined values) |
| Typed literals | `DateTimeOffset` (ISO-8601 round-trip), `Edm.Date` (`yyyy-MM-dd`), `Guid`, `bool`, numerics (invariant), enum member names |

Explicitly rejected — `400 Bad Request` with an actionable detail and the
`granit.odata.query.rejected` metric (`reason=filter_not_translatable`):

- `any(...)` / `all(...)` lambdas and collection `$count` segments
- arithmetic operators (`add`, `sub`, `mul`, `div`, `mod`) and unary minus
- the `has` flag-enum operator
- cross-navigation property access (`Customer/Name`) — filter on the
  EntitySet's own columns
- every other canonical function (`tolower`, `toupper`, `trim`, `concat`,
  `indexof`, `length`, `substring`, `year`, `month`, `day`, `date`, `time`,
  `now`, `round`, `floor`, `ceiling`, `cast`, `isof`, `geo.*`, …)
- dynamic (open-type) properties
- `in` list items that would not round-trip the engine's comma-separated
  list format (items containing a comma, `null`, empty or
  whitespace-padded strings)

The 400 contract (RFC 7807 Problem, title `Query option not supported`):

- **`filter_not_translatable`** — the construct is not in the table above;
  the detail names it and suggests the supported alternative.
- **`filter_field_rejected`** — the filter is translatable but references
  columns the `QueryDefinition` does not allow; the detail lists every
  violation (`[FieldNotFilterable] InternalNote: …`).
- **`odata_validation_failed`** — the clause failed to parse, referenced a
  property absent from `$metadata`, or another query option failed the
  per-route validation settings (e.g. `$orderby` on a non-`Sortable()`
  column).

> **BREAKING (#3004):** filtering on an EDM-visible but non-`Filterable()`
> column previously passed through `ApplyTo` silently; it now returns
> `400` with the field name in the Problem detail. Mark every column BI
> tools filter on as `Filterable()` in the `QueryDefinition`. Similarly,
> `$orderby` now requires the column to be `Sortable()`.

## `$expand` support (#3005)

`ExpandWhitelist(...)` takes **dotted navigation paths**:

```csharp
opts.EntitySet<Invoice, InvoiceQueryDefinition>("Invoices")
    .RequirePermission("OData.Invoicing.Invoices.Read")
    .ExpandWhitelist("Customer", "Customer.Address", "Lines")
    .MaxExpansionDepth(2);   // "Customer.Address" is 2 levels deep
```

Semantics:

- A **top-level entry** (`"Customer"`) allows expanding that navigation —
  scalars only, no nested `$expand` beneath it.
- A **dotted entry** (`"Customer.Address"`) additionally allows the nested
  expand `Customer($expand=Address)`. Prefixes of a whitelisted path are
  implicitly allowed (`$expand=Customer` alone works).
- Matching is **case-insensitive**, and `$levels` literals are normalised
  into repeated segments — `Manager($levels=2)` walks the same gates as
  `Manager($expand=Manager)` (path `Manager.Manager`).

**Startup gates** (strict-config, fail-fast at `Map*` time):

1. Every path segment must exist as a navigation property on the CLR type
   it is declared on — a typo or a scalar segment refuses to start the
   host (pre-#3005 it was dead config producing raw parser errors).
2. Every navigation-target type reachable through a whitelisted path must
   have a registered `ExportDefinition` — its scalar fields become the
   target's EDM whitelist. No export, no exposure (ADR-050).

**Request-time gates** — the parsed `$expand` AST is walked recursively;
each expanded navigation is checked as a dotted path against the resolved
whitelist and against `MaxExpansionDepth` (default `1`, flat expand only).
Rejections are `400` Problems tagged on `granit.odata.query.rejected`:

- **`expand_not_whitelisted`** — the path is not in the whitelist (or the
  set disabled expand entirely).
- **`expand_depth_exceeded`** — nesting (or `$levels`) goes deeper than
  `MaxExpansionDepth`. `$levels=max` always exceeds a finite cap.
- **`odata_validation_failed`** — the clause failed to parse (e.g. a
  navigation absent from the EDM).

`ODataValidationSettings.MaxExpansionDepth` is set from the descriptor as
well — belt and braces behind the AST check.

## `$metadata` / service-document authorization stance (#3005)

`$metadata` and the service document disclose the mount's full schema —
EntitySet names, columns, navigations. That is reconnaissance material, so
every mount MUST declare an explicit stance; there is no default:

- `opts.RequireMetadataPermission("OData.{Module}.Metadata.Read")` — both
  documents require an authenticated caller holding the permission
  (`401` unauthenticated, `403` authenticated without it).
- `opts.AllowAnonymousMetadata()` — both documents are public, by design
  (public reference-data feeds, or BI connectors that probe the service
  document before sign-in).

Declaring neither is a startup error. On the **host-feed**,
`RequireMetadataPermission(...)` is REQUIRED — there is no anonymous
variant, and the permission must resolve to `MultiTenancySides.Host`
exactly like the per-set entity permissions.

## Strict-config validator (C6 #1395, extended by #3005)

Every EntitySet MUST explicitly declare two security-sensitive intents, and
every mount a third:

1. **Permission** — call `.RequirePermission(...)` (gated) **OR** `.AllowAnonymousAccess()` (public-feed scenario, e.g. tenant-agnostic reference data).
2. **`$expand` policy** — call `.ExpandWhitelist(...)` (allow listed navigation paths) **OR** `.DisableExpand()` (no navigation exposed). Whitelisted paths are validated at startup (see [`$expand` support](#expand-support-3005)).
3. **`$metadata` stance** (mount-level) — `.RequireMetadataPermission(...)` **OR** `.AllowAnonymousMetadata()`.

Without each of these, `MapGranitODataEndpoints` throws at host startup with a list of every misconfiguration. The framework deliberately does NOT default-deny silently — silent defaults let convention drift reach production unchecked. Failing fast at composition time is the equivalent of an architecture test for a config surface that lives inside a closure (and is therefore not statically reflectable).

## Hardening (per EntitySet)

The fluent builder ships safe-by-default caps; override per set when the
data product warrants it:

```csharp
opts.EntitySet<Invoice, InvoiceQueryDefinition>("Invoices")
    .RequirePermission("OData.Invoicing.Invoices.Read")  // strict-config: required
    .ExpandWhitelist("Customer", "Customer.Address")     // strict-config: required (or DisableExpand())
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
- **`$expand=<path>`** — returns `400 Bad Request` if the dotted path is
  not in `ExpandWhitelist` or nests deeper than `MaxExpansionDepth`. An
  empty whitelist (or no call at all) disables expand entirely. See
  [`$expand` support](#expand-support-3005).
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
    opts.RequireMetadataPermission("OData.Host.Platform.Metadata.Read");  // REQUIRED — no anonymous variant

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

Four strict-config gates are added on top of the tenant-feed validator:

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
4. **`RequireMetadataPermission(...)` mandatory (#3005).** The host-feed
   `$metadata` / service document expose the cross-tenant BI schema; the
   permission is validated `MultiTenancySides.Host` like the entity
   permissions, and there is no `AllowAnonymousMetadata()` on this surface.

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
