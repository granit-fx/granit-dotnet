---
title: "ADR-048: Cross-module entity relations (smart buttons + sidebar + tab + inline-chips)"
description: "An entity gets cross-module 'smart button' badges (and sidebars, tabs, inline-chip rows) grafted from other modules via the third application of the IoC contributor pattern (ADR-045). Four display modes cover 95% of UX needs. Aggregates batched into one round-trip endpoint to avoid N+1. Permission filtering at both manifest emission and aggregate computation — defense in depth. FusionCache 30s with entity-tagged invalidation hooks reserved for Phase 2."
sidebar:
  order: 48
  label: "048 - Cross-Module Entity Relations"
---

> **Date:** 2026-04-30
> **Authors:** Jean-Francois Meyers
> **Scope:** granit-dotnet (`Granit.Entities.Abstractions`, `Granit.Entities`, `Granit.Entities.Endpoints`); granit-front (`@granit/entities-react` smart-button bar)
> **Epic:** [#1506](https://github.com/granit-fx/granit-dotnet/issues/1506) — Refonte UI Hybride
> **Story:** [#1527](https://github.com/granit-fx/granit-dotnet/issues/1527) — ADR Cross-module entity relations
> **Status:** Accepted

## Context

Odoo's "smart buttons" are the most copied UX in ERP-class admin frameworks: at the top of a Customer detail page, a row of badges shows `5 Opportunities`, `12 Sales`, `8 Invoices`, `3 Tasks`. Each opens a filtered list of the related records. The badges appear **only if the corresponding module is installed** (`Sales` adds the Sales button; `CRM` adds the Opportunities button; etc.). The mechanism is XML view inheritance — every module XPath-patches the customer form to inject its badge.

Frappe ships a dimmer cousin: "Connections" sidebar, auto-generated from `Link` fields declared with `connections: 1`. Lighter (auto-discovered, no XML), less prominent (sidebar not header), no aggregates (count only — no sum / sum-by-currency).

Both have known liabilities — documented in PR #1599 conversation:

- Odoo's XPath inheritance produces brittle anchors; one module's `position="replace"` silently nukes another's badge across install combinations.
- Frappe's auto-discovery is convention-driven and limited (no aggregations beyond count, no per-display-mode choice).
- Both perform N+1 queries unless every count is a stored / computed field — at which point recompute cascades become a bottleneck.

Granit needs the smart-button UX with **typed C# contributions** (per [ADR-045](./045-contributor-pattern)), **multiple display modes** (badge / sidebar / tab / inline-chips), **batched aggregates** (one round-trip per detail load), and **defense-in-depth permission filtering** (consistent with [ADR-040](./040-three-tier-metadata-architecture)).

## Decision

### 1. Two declaration sites — intra-module + cross-module

**Intra-module** (the entity's own owning module declares relations to its own data — e.g., `Granit.Parties.Party` has many `PartyAddresses`):

```csharp
public sealed class PartyEntityDefinition : EntityDefinition<Party>
{
    protected override void Configure(EntityDefinitionBuilder<Party> b)
    {
        b.HasMany<PartyAddress>(p => p.Addresses)
         .DisplayAs(RelationDisplay.Tab)
         .DisplayKey("Relation:Party.Addresses");
    }
}
```

**Cross-module** (another module grafts a relation onto an entity it doesn't own — e.g., `Granit.Invoicing` adds the Invoices smart button onto `Party`), via the third consumer of the IoC contributor pattern (per [ADR-045](./045-contributor-pattern)):

```csharp
// In Granit.Invoicing.Endpoints
public sealed class InvoicesOnPartyRelationContribution : IEntityRelationContributor
{
    public string TargetEntity => "Granit.Parties.Party";
    public int Order => 200;
    public string? RequiredPermission => "Invoicing.Invoices.Read";

    public void Contribute(EntityRelationContext ctx) =>
        ctx.AddRelation<Party, Invoice>("invoices", r => r
            .DisplayKey("Relation:Party.Invoices")
            .Icon("file-text")
            .Display(RelationDisplay.SmartButton)
            .Where((party, invoice) => invoice.CustomerId == party.Id)
            .Aggregate(a => a
                .Count()
                .Sum(i => i.AmountTotal, ValueKind.Currency)
                .Count("overdue", i => i.IsOverdue))
            .NavigateTo<Invoice>(view: "default",
                preset: ctx => new InvoiceListPreset { CustomerId = ctx.SourceId }));
}

// Module DI
services.AddEntityRelationContribution<InvoicesOnPartyRelationContribution>();
```

### 2. Four display modes — closed catalog

| Mode | Where it renders | When to use |
| ---- | ---------------- | ----------- |
| `SmartButton` | Prominent badge bar at the top of the detail (Odoo-style) | Primary KPIs the user wants at-a-glance: counts + sums for related collections |
| `Sidebar` | Discreet entry in the right-hand sidebar (Frappe-style) | Secondary relations the user wants to know exist but doesn't open every visit |
| `Tab` | Embedded grid as a tab inside the detail (intra-module collections) | Owned child collections that belong to the parent's editing context (InvoiceLines, Addresses) |
| `InlineChips` | Chip row inline with the detail content | Many-to-many relations where the items are short labels (Tags, Categories) |

This is the **v1 catalog** — same closed-set + `custom:` namespace pattern as [ADR-041](./041-component-catalog) §2 / [ADR-042](./042-view-catalog) §2. Adding a new display mode requires an ADR amendment + renderer in `@granit/entities-react`.

### 3. Aggregates — closed primitive set

Each relation declares zero or more aggregates the framework computes server-side per request:

| Primitive | Sample declaration | Wire payload field |
| --------- | ------------------ | ------------------ |
| Count | `.Count()` | `count: number` |
| Filtered count | `.Count("overdue", i => i.IsOverdue)` | `subCounts: { overdue: number }` |
| Sum | `.Sum(i => i.AmountTotal, ValueKind.Currency)` | `sum: number, currency: "EUR" \| null` |
| Avg | `.Avg(i => i.PaymentDelayDays)` | `avg: number` |
| Min | `.Min(i => i.IssuedAt)` | `min: any` |
| Max | `.Max(i => i.IssuedAt)` | `max: any` |

Aggregates compose via `MetricDefinition` machinery internally (per [ADR-038](./038-analytics-dashboard-definition-vs-aggregate)) — the same SQL projection rules, the same per-aggregate caching policy, the same `ValueKind` propagation for the frontend's currency / count / percent formatting.

### 4. Batched aggregates endpoint — one round-trip per detail load

The naive shape (one HTTP call per relation) is N+1 from the frontend's perspective: 5 smart buttons on a Party detail = 5 sequential round-trips. Granit ships a dedicated endpoint:

```http
POST /api/entities/{name}/{id}/relations/aggregates
Content-Type: application/json

{ "relations": ["invoices", "subscriptions", "payments", "balance"] }
```

Response:

```json
{
  "invoices":      { "count": 12, "sum": 45230.00, "currency": "EUR", "subCounts": { "overdue": 3 } },
  "subscriptions": { "count": 2,  "sum": 199.00,   "currency": "EUR" },
  "payments":      { "count": 47, "sum": 12480.00, "currency": "EUR" },
  "balance":       { "count": 1,  "sum": -230.00,  "currency": "EUR" }
}
```

Server side:

- Each relation's aggregate runs as **one SQL query**, composed via `Task.WhenAll` for parallel execution.
- The relations the user lacks `RequiredPermission` for are **dropped from the request** before SQL — never queried, never returned.
- One OpenTelemetry span per aggregate; the parent span carries the batched request id.

### 5. Permission filtering — defense in depth, two stages

Stage 1 — **manifest emission**: the relation absent from the entity manifest's `relations` array entirely. The frontend never knows the smart button exists.

Stage 2 — **aggregate batched endpoint**: even if the frontend asks for `relations: ["invoices"]` somehow (hand-crafted request, stale manifest, dev tools), the server drops the relation from the response. Never just hides; never just zeroes. Absent.

Same defense-in-depth contract as Phase 1 story #1549 (manifest payload permission filtering). A user without `Invoicing.Invoices.Read` cannot even infer that `Granit.Invoicing` is loaded by inspecting smart-button counts.

### 6. FusionCache + entity-tagged eviction (eviction wired in Phase 2)

Cache key: `relation-agg:{sourceEntity}:{sourceId}:{relationName}:{userPermsHash}`. Sliding 30 s — counts change frequently; longer cache risks stale UX, shorter cache risks hammering the DB on rapid Detail navigations.

Eviction tag: `entity:{sourceEntity}:{sourceId}`. Phase 1 (#1562) wires the cache reads + writes; **Phase 2 (under #1510)** wires event-driven invalidation: a write on `Invoice` for `CustomerId = X` invalidates `entity:Granit.Parties.Party:X`, dropping the stale Invoices smart-button count for every user viewing that Party.

### 7. Conditional visibility — `VisibleIf` on relations

A relation can be conditionally hidden based on the source entity's state:

```csharp
ctx.AddRelation<Party, Invoice>("invoices", r => r
    .VisibleIf(p => p.IsBillable)            // smart button hidden for non-billable parties
    .Display(RelationDisplay.SmartButton)
    .Aggregate(a => a.Count()));
```

`VisibleIf` uses the same closed-enum operator catalog as field visibility (per ADR-041's `VisibleIf` DSL: `Eq`, `NotEq`, `In`, `NotIn`, `Gt`, `Lt`, `IsNull`, `IsNotNull`). Evaluated server-side at manifest emission; the relation is absent from the emitted manifest when the source entity instance fails the condition.

### 8. Cobaye for Phase 1 — Party + 4 cross-module contributions

Phase 1 (#1563 cobaye implementation) demonstrates the pattern end-to-end on `Party`:

- `Granit.Invoicing.Endpoints` → `Invoices` smart button (count + sum AmountTotal + sub-count overdue)
- `Granit.Subscriptions.Endpoints` → `Subscriptions` smart button (count active + sum MRR)
- `Granit.Payments.Endpoints` → `Payments` smart button (count + sum)
- `Granit.CustomerBalance.Endpoints` → `Balance` smart button (single-value display)

Plus the intra-module `Addresses` tab on `Party`. Five total relations on a single detail page; one batched round-trip; permission filter respected end-to-end.

## Consequences

### Positive

- Pattern parity with Odoo's smart buttons + Frappe's connections — without their structural liabilities.
- Typed C# contributions (per ADR-045) ⇒ refactoring, IDE navigation, compile-time safety, no XPath fragility.
- Four display modes covering distinct UX needs (prominent KPI / discreet sidebar / embedded tab / inline chips) — apps pick the right surface per relation.
- Batched aggregates endpoint ⇒ one round-trip per detail load regardless of how many modules contribute.
- Permission filtering at two stages ⇒ neither the smart button nor its count leaks information about installed modules.
- FusionCache with entity-tagged eviction ⇒ counts stay fresh after writes (Phase 2) without hammering the DB on rapid navigation.

### Negative / accepted trade-offs

- The cross-module contributor pattern means the "owning" module of a relation (e.g., `Granit.Invoicing` for the Invoices-on-Party smart button) ships UI metadata the source module (`Granit.Parties`) never sees. This is a feature, not a bug — but it requires devs to know "where do I add a smart button?" → "the contributing module's Endpoints layer". Documentation must be explicit.
- Conditional visibility (`VisibleIf`) evaluated server-side requires loading the source entity to evaluate the condition. Mitigated by piggy-backing on the detail load (the entity is already in memory).
- Aggregate eviction wiring is Phase 2 — Phase 1 ships sliding 30 s cache only. Acceptable trade-off: Phase 1 is the cobaye; Phase 2 hardens.

## Cross-references

- [ADR-040](./040-three-tier-metadata-architecture) — Three-tier metadata. Relations are Tier A (compiled); tenant admins cannot add cross-module relations from the UI.
- [ADR-038](./038-analytics-dashboard-definition-vs-aggregate) — Aggregate computation reuses `MetricDefinition` machinery internally; no parallel pipeline.
- [ADR-041](./041-component-catalog) §2 — Same closed-set + `custom:` namespace pattern, applied to display modes.
- [ADR-045](./045-contributor-pattern) — IoC contributor pattern. `IEntityRelationContributor` is the third primitive served (after `IWorkspaceContributor` and `IActivityTypeProvider`).

## References

- Odoo smart buttons + XPath view inheritance — research compiled in PR #1599 conversation. Brittle anchors, install-order surprises, performance issues with non-stored counts.
- Frappe Connections — auto-discovered from `Link` fields. Lighter UX (sidebar only), no aggregates beyond count, no per-display-mode choice.
- Salesforce Related Lists — analogous to Granit's `Tab` mode for embedded child collections; less flexibility on aggregations.
- Notion linked databases — adopted as the conceptual "view of related data, scoped to context" pattern; Granit's batched aggregates endpoint and explicit display modes go further on the structural side.
