---
title: "ADR-038: DashboardDefinition vs Dashboard Boundary"
description: "Modules ship DashboardDefinition (declarative, in-code, immutable). Tenants persist Dashboard (aggregate root, mutable, composed by admins). Dashboards are imported on demand, not auto-instantiated. Once instantiated, they are frozen — module upgrades do not retro-edit user dashboards."
sidebar:
  order: 38
  label: "038 - Dashboard Definition vs Aggregate"
---

> **Date:** 2026-04-28
> **Authors:** Jean-Francois Meyers
> **Scope:** granit-dotnet (Granit.Analytics, Granit.Analytics.EntityFrameworkCore, Granit.Analytics.Endpoints)
> **Epic:** [#1366](https://github.com/granit-fx/granit-dotnet/issues/1366) — Business Intelligence
> **Story:** [#1381](https://github.com/granit-fx/granit-dotnet/issues/1381) — B0

## Context

Feature B of the BI epic introduces "composable dashboards" on top of the inline-metric
foundation shipped in Feature A (`MetricDefinition` + `POST /metrics/{name}`). The word
*dashboard* covers two distinct concepts that were conflated in early design notes:

1. **What a module ships out of the box.** "The Invoicing module proposes a *Finance
   overview* dashboard with these 7 widgets and these defaults." Lives in code, like
   `QueryDefinition` / `MetricDefinition` / `ExportDefinition` (ADR-020). Never mutated
   at runtime.
2. **What an admin actually composes for their tenant.** Persisted, mutable. Layout
   customised, widgets added/removed/reconfigured, permissions per widget enforced at
   render time.

Treating the two as one type creates immediate problems:

- Module upgrades would silently mutate a tenant's curated layout — a regression risk
  comparable to a CMS template editing the user's published page.
- Admins could not reorder, rename or remove widgets on a "shipped" dashboard without
  forking the module's code.
- The persistence model would force every framework module to ship migrations, which
  contradicts the Granit "no migrations in framework packages" rule.

Mirror of the `Granit.QueryEngine` precedent: `QueryDefinition<T>` is declarative and
in-code; the *runtime query* (`QueryRequest` payload + applied filter pipeline) is the
mutable counterpart on the wire. Dashboards follow the same dichotomy.

## Decision

### 1. Two types, two homes

| Concept | Type | Lives in | Mutability | Persistence |
| ------- | ---- | -------- | ---------- | ----------- |
| Module-shipped catalogue entry | `DashboardDefinition` | `Granit.{Module}` (base module, alongside `QueryDefinition` / `ExportDefinition`) | Immutable — code | None — assembly only |
| Tenant-composed dashboard | `Dashboard` | `Granit.Analytics` (aggregate root) | Mutable — admin actions | `Granit.Analytics.EntityFrameworkCore` |

`DashboardDefinition` follows the placement rule of ADR-020: each module owns its own
catalogue entries and registers them via `services.AddDashboardDefinition<TDef>()`.
There is no central aggregation package.

### 2. Dashboards are *imported*, not auto-instantiated

When a tenant first installs a module, **no `Dashboard` row is created automatically**.
The admin browses the catalogue (`GET /dashboards/catalog`) and explicitly imports a
definition (`POST /dashboards/from-definition/{name}`), which **deep-copies** the
definition into a new `Dashboard` aggregate.

Justification:

- Auto-instantiation would dump dozens of dashboards on every tenant the moment a
  module ships — cognitive overload, no signal of intent.
- Explicit import gives admins the right reflex: "this is mine now; the module no
  longer drives it."
- Symmetry with how email templates already work in `Granit.Notifications` (admins
  override embedded templates by explicitly persisting their version).

### 3. Imported dashboards are frozen

Once a `Dashboard` is created from a `DashboardDefinition`, **module upgrades do NOT
retro-edit it**. The shipped definition becomes the *seed*; the persisted dashboard is
the *living copy* and evolves independently.

Drift is surfaced — not silently reconciled — via two mechanisms:

- `Dashboard.SourceDefinitionVersion` records the version of the definition at import
  time. Admin UI can flag "the *Finance overview* shipped by Invoicing has been updated
  since you imported it" and offer a manual *re-sync* action that the admin must trigger.
- A `Dashboard.IsSystem` flag (see decision 5) reserves a one-way escape hatch for
  framework-critical dashboards.

Justification:

- A tenant invests effort customising layout and widget configuration. Silently
  overwriting that on `dotnet ef database update` would be a CMS-grade regression.
- Re-sync is an admin-driven UX, not a migration concern. It belongs at the
  application layer, not in EF Core.

### 4. Dashboards may reference widgets outside any definition

A `Dashboard` is **not** constrained to widgets present in some `DashboardDefinition`.
Admins compose freely — any widget pointing to any metric / query they have permission
on may be added.

Justification:

- The module-shipped definition is a *suggestion*, not a contract. Admins know their
  business better than module authors.
- The pairing rule already enforced by D1 (`MetricDefinition` ↔ `QueryDefinition`) and
  by ADR-020 (`QueryDefinition` ↔ `ExportDefinition`) guarantees that any metric an
  admin could pin has a queryable counterpart and an exportable counterpart.
  Permission filtering at render time (story B4) closes the security loop.

### 5. `IsSystem` flag for framework-critical dashboards

A `DashboardDefinition` may set `IsSystem = true`. When imported, the resulting
`Dashboard` carries the flag and **cannot be deleted** by admin UI (only re-sync).

Use cases:

- An ops dashboard imposed by the platform operator (multi-tenant SaaS host) that the
  tenant must keep visible — typically tied to a contractual SLA dashboard.
- A compliance dashboard required by a regulated industry deployment.

`IsSystem` is **not** the default; framework modules ship dashboards with
`IsSystem = false`. The flag is a deliberate opt-in for hosting scenarios.

### 6. Persistence model

```csharp
// Aggregate root
class Dashboard : AuditedAggregateRoot, IMultiTenant, ISoftDelete
{
    public DashboardId Id { get; private set; }
    public Guid? TenantId { get; private set; }
    public string Name { get; private set; }                  // tenant-renamable
    public string? Slug { get; private set; }                 // /dashboards/{slug}
    public string? SourceDefinitionName { get; private set; } // null = ad-hoc
    public string? SourceDefinitionVersion { get; private set; }
    public bool IsSystem { get; private set; }
    public DashboardLayout Layout { get; private set; }       // value object — grid/columns/breakpoints
    public IReadOnlyList<WidgetInstance> Widgets => _widgets;
    private readonly List<WidgetInstance> _widgets = [];
}

// Owned entity — one row per pinned widget, ordered
class WidgetInstance : AuditedEntity
{
    public WidgetInstanceId Id { get; private set; }
    public DashboardId DashboardId { get; private set; }
    public int Position { get; private set; }                 // dense-ranked
    public WidgetKind Kind { get; private set; }              // Kpi | Chart | Table | Pivot | Markdown | Map
    public string? MetricName { get; private set; }           // for Kpi / Chart
    public string? QueryDefinitionName { get; private set; }  // for Table / Pivot
    public string ConfigJson { get; private set; }            // kind-specific JSON: ChartType, ColumnSet, ...
    public string? RequiredPermission { get; private set; }   // optional override; default derived from Metric/Query
    public WidgetSize Size { get; private set; }              // value object — { width, height } in grid units
}
```

Notes:

- `WidgetInstance.ConfigJson` is intentionally a JSON blob, **not** a polymorphic
  hierarchy — keeps the schema additive when new widget kinds ship (story B7 will add
  `Map` without a migration). Validated server-side by a per-`WidgetKind` validator.
- `RequiredPermission` is **optional**. When absent, the runtime resolves the effective
  permission from the linked metric / query — keeping the most-restrictive contract
  without forcing the admin to retype it on every widget. The override exists for
  composite widgets (e.g. a Kpi that surfaces an aggregate of two metrics under a
  single coarser permission).
- Soft-delete is applied to dashboards (admins can restore an accidentally-deleted
  dashboard during a 30-day window via the standard `IDataFilter.Disable<ISoftDelete>()`
  scope).
- No domain events on `Dashboard` for now (story B2). Re-sync orchestration in B6 may
  introduce them.

`DashboardDefinition` mirrors the same shape but with declarative, immutable
counterparts:

```csharp
abstract class DashboardDefinition
{
    public abstract string Name { get; }                      // "Granit.Invoicing.FinanceOverview"
    public abstract string DisplayKey { get; }                // "Dashboard:Granit.Invoicing.FinanceOverview"
    public abstract string Version { get; }                   // semver — used for drift detection
    public virtual bool IsSystem => false;
    public abstract DashboardLayout Layout { get; }
    public abstract IReadOnlyList<WidgetDefinition> Widgets { get; }
}

abstract record WidgetDefinition(
    int Position,
    WidgetKind Kind,
    string? MetricName,
    string? QueryDefinitionName,
    string ConfigJson,
    string? RequiredPermission,
    WidgetSize Size);
```

### 7. Migration story — definition removed in module upgrade

When a module upgrade removes a `DashboardDefinition` that some tenants imported:

1. The persisted `Dashboard` is **not affected** (it was deep-copied at import time).
   The dashboard continues to render exactly as before.
2. Any `WidgetInstance` whose `MetricName` / `QueryDefinitionName` no longer resolves
   renders as a *widget unavailable* placeholder card, listing the missing reference
   and a "remove this widget" action. The dashboard itself stays usable; only the
   broken widgets degrade.
3. The catalogue endpoint (`GET /dashboards/catalog`) silently stops listing the
   removed definition. The drift-detection UI flags imported dashboards whose
   `SourceDefinitionName` no longer exists — admin can choose to keep them as ad-hoc
   (clearing `SourceDefinitionName` / `SourceDefinitionVersion`) or delete them.

Rationale: the framework cannot proactively delete tenant-owned data on a code change.
Graceful degradation + admin-driven cleanup matches the wider Granit principle
([feedback] no destructive framework migrations) and the regulatory baseline (GDPR
data minimisation does not authorise the framework to wipe user-created records).

## Evaluated alternatives

### Alternative A — single mutable `Dashboard` shared across tenants

**Rejected.** Re-introduces the conflation. A multi-tenant SaaS where any admin's
edit affects every tenant breaks isolation entirely.

### Alternative B — auto-instantiate one `Dashboard` per `DashboardDefinition` on first login

**Rejected.** Overwhelms tenants with dashboards none of them asked for. Prior art
(both Odoo and Power BI App Workspaces) shows that explicit import drives adoption,
auto-instantiation drives noise.

### Alternative C — track-the-source dashboards (module upgrades retro-edit user copies)

**Rejected.** Silent retro-edit on `database update` is a CMS-grade regression risk.
Drift detection + manual re-sync gives admins the agency they need, while still
surfacing the upgrade.

### Alternative D — typed widget hierarchy (one C# class per `WidgetKind`) instead of `ConfigJson`

**Rejected.** Every new widget kind would force a migration, contradicting the *no
migrations in framework packages* rule. JSON blob + per-kind validator gives the same
type safety at the API layer with an additive schema. Weighed and accepted as the
classic "JSON column for polymorphic config" trade-off — see EF Core 10 owned-entity
guidance.

### Alternative E — store widget order via a linked-list (`PreviousId` / `NextId`)

**Rejected.** Reorder operations would touch O(n) rows. A dense-ranked `Position`
integer with periodic compaction matches the workload (a dashboard rarely exceeds 30
widgets) and keeps reorder updates to two rows.

## Justification

- **Mirror of established framework patterns.** `DashboardDefinition` follows
  `QueryDefinition` / `ExportDefinition` / `MetricDefinition` (declarative, in-code,
  per-module ownership, registered via DI extension method). Predictable for module
  authors.
- **Mirror of established UX patterns.** Import + freeze + drift detection matches
  `Granit.Templating` (embedded templates can be overridden by an admin-persisted
  copy) and `Granit.Notifications` (subscription tables are populated explicitly, not
  auto-generated).
- **Tenant data sovereignty.** Once an admin invests effort composing a dashboard,
  the framework treats it as tenant-owned and never silently mutates it.
- **Additive schema.** JSON `ConfigJson` + dense-ranked `Position` keeps story B7
  (`MapWidget`) and any future widget kind shippable without a migration in
  `Granit.Analytics.EntityFrameworkCore`.

## Consequences

### Positive

- Module upgrades are safe. Framework authors ship new versions of `DashboardDefinition`
  without fear of clobbering tenant data.
- Drift detection becomes a first-class admin UX rather than a hidden migration.
- Widget kinds are pluggable per story (B3, B7, ...) with no schema impact.
- The `IsSystem` flag gives multi-tenant SaaS hosts a clean way to impose mandatory
  ops dashboards without abusing tenant overrides.

### Negative

- Two types instead of one — admins/devs must remember which one they are dealing
  with. Mitigated by clear naming (`Dashboard` vs `DashboardDefinition`) and by
  putting them in the same module so IDE auto-complete keeps them adjacent.
- Drift between definition and persisted copy must be surfaced in the admin UI.
  Story B5 (frontend composer) is responsible for this UX. Until B5 ships, drift
  is queryable via API but not visualised.
- Story B4 (endpoints) must implement the deep-copy logic on import, including
  permission resolution per widget. This is intentional and matches the pairing
  invariants enforced by D1 and ADR-020.

### Migration order

1. **B1** (#1382) — implement `DashboardDefinition`, `WidgetDefinition`, `WidgetSize`,
   `DashboardLayout` value objects in `Granit.Analytics`. Add
   `services.AddDashboardDefinition<TDef>()` extension. Architecture test enforces
   placement (base module, not `.Endpoints`).
2. **B2** (#1383) — implement `Dashboard` aggregate, `WidgetInstance` owned entity,
   EF configurations, migration in `Granit.Analytics.EntityFrameworkCore`. Apply
   `ApplyGranitConventions` (multi-tenant + soft-delete).
3. **B3** (#1384) — ship the five baseline widget kinds (Kpi, Chart, Table, Pivot,
   Markdown) with per-kind `ConfigJson` validators.
4. **B4** (#1385) — REST endpoints (`GET /dashboards/catalog`,
   `POST /dashboards/from-definition/{name}`, full CRUD on persisted dashboards) +
   permission filtering at render time.
5. **B5** (#1386) — frontend composer + read-mode rendering with drift indicator.
6. **B6** (#1387) — docs-site page mirroring this ADR for end users.
7. **B7** (#1404) — `MapWidget` (Leaflet) — additive, no schema change.

## References

- [`CLAUDE.md` — Analytics conventions section](../../../../../../../CLAUDE.md)
- ADR-017 — [DDD Aggregate Root & Value Object Strategy](./017-ddd-aggregate-value-object-strategy.md)
- ADR-018 — [FusionCache Caching Provider](./018-fusioncache-caching-provider.md)
- ADR-020 — [Declarative Definitions Placement (Query & Export)](./020-declarative-definitions-placement.md)
- Epic [#1366](https://github.com/granit-fx/granit-dotnet/issues/1366) — Business Intelligence
- Feature [#1368](https://github.com/granit-fx/granit-dotnet/issues/1368) — Layer 2: Composable dashboards
