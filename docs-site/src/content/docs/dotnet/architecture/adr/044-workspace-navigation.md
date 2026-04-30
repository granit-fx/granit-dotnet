---
title: "ADR-044: Workspace navigation"
description: "Granit ships hierarchical workspaces (à la Frappe) with cross-workspace entity sharing via additive preset overlays (à la Odoo Apps). Detail routes are workspace-agnostic (à la Notion). Permissions intersect: a workspace item is visible iff the user has both Workspace.{Name}.Read AND the underlying entity's read permission. Depth is capped at 4 by an architecture test."
sidebar:
  order: 44
  label: "044 - Workspace Navigation"
---

> **Date:** 2026-04-30
> **Authors:** Jean-Francois Meyers
> **Scope:** granit-dotnet (`Granit.Workspaces`, `Granit.Workspaces.Endpoints`, `Granit.Workspaces.Framework`); granit-front (`@granit/workspaces-react`)
> **Epic:** [#1506](https://github.com/granit-fx/granit-dotnet/issues/1506) — Refonte UI Hybride
> **Story:** [#1523](https://github.com/granit-fx/granit-dotnet/issues/1523) — ADR Workspace navigation
> **Status:** Accepted
>
> **Numbering note:** the planning conversation reserved ADR-043 for this topic, but [PR #1618](https://github.com/granit-fx/granit-dotnet/pull/1618) shipped ADR-043 (Dashboard push transport) on the same day. This ADR slots in at 044; the remaining Phase 0 ADRs shift accordingly (045 IoC contributor, 046 Activities/Timeline split, 047 EntityView, 048 Cross-module relations, 049 Landing route). Cross-references in the already-merged ADR-040, ADR-041, ADR-042 are amended in the same PR that lands this file.

## Context

Frappe ships **Workspaces** as the primary navigation primitive: a hierarchical tree where each workspace exposes its own sidebar of items (DocTypes, dashboards, reports, links). Sub-workspaces nest arbitrarily (`Accounting > Invoicing > Sales Invoices`). Tenant admins reorder, rename, hide.

Odoo ships **Apps** as a flatter alternative: a single level of top-tabs, each tab gates a menu tree underneath. Critically, the same model can appear under multiple apps — the customer list shows up under Accounting, Sales, and Contacts, each time with the app's preset filter (e.g., Accounting hides leads).

Notion uses **page hierarchy** for navigation; the side peek + canonical URL split between "this page in this context" and "this page anywhere" is one of its strongest UX patterns.

Granit's planning conversation locked the synthesis: **Frappe-style hierarchy + Odoo-style cross-workspace sharing + Notion-style canonical detail routes**, all driven by the inversion-of-control contributor pattern (see [ADR-045](./045-contributor-pattern)) so that adding a module never requires patching a workspace declared elsewhere.

## Decision

### 1. Hierarchical workspace tree

`WorkspaceDefinition` declares a workspace by name with optional sub-workspaces, ordered sections, and items per section:

```csharp
public sealed class AccountingWorkspaceDefinition : WorkspaceDefinition
{
    public override string Name => "Granit.Accounting";
    public override string DisplayKey => "Workspace:Accounting";
    public override string Icon => "calculator";
    public override int Order => 100;
    public override string? PermissionPrefix => "Accounting";

    protected override void Configure(WorkspaceBuilder b)
    {
        b.SubWorkspace<InvoicingWorkspaceDefinition>();
        b.SubWorkspace<PaymentsWorkspaceDefinition>();

        b.Section("masters", s => s
            .Entity<Customer>("active-with-balance",
                preset: p => p
                    .Filter("HasOpenInvoices", FieldOp.Eq, true)
                    .Sort("-LastInvoiceDate")
                    .Columns("Name", "Email", "OutstandingAmount"))
            .Entity<Supplier>("default"));

        b.Section("dashboards", s => s
            .Dashboard<FinanceOverviewDashboard>()
            .Dashboard<CashFlowDashboard>());
    }
}
```

The discovery endpoint (`GET /api/workspaces`, story #1554) returns the tree filtered by the user's permissions.

### 2. Depth ≤ 4, enforced

A workspace tree may nest at most **4 levels deep** (`Root > Sub > SubSub > SubSubSub`). The cap is enforced by an architecture test in `Granit.ArchitectureTests` (the `AbstractionsPurityTests` infrastructure introduced in [PR #1603](https://github.com/granit-fx/granit-dotnet/pull/1603) extends naturally to a `WorkspaceDepthTests`).

Rationale: deeper trees collapse the UX into a tunnel — Frappe ships ad-hoc workspaces beyond 3 levels and the result is a navigation maze. Four levels accommodate the Granit Framework workspace pattern (`Framework > System > Settings > <module item>`) and stops there.

### 3. Cross-workspace entity sharing

The same entity may be exposed in N workspaces, each carrying its own preset overlay:

```csharp
// Granit.Accounting workspace — Customer with overdue-balance preset
b.Section("masters", s => s.Entity<Customer>("active-with-balance",
    preset: p => p.Filter("HasOpenInvoices", FieldOp.Eq, true)));

// Granit.Contacts workspace — Customer with full list, no preset
b.Section("contacts", s => s.Entity<Customer>("default"));
```

Same `EntityDefinition`, same `QueryDefinition`, two different surfaces.

### 4. Additive overlay only — security invariant

The preset overlay is **strictly additive**: it can constrain (`AND` more filters, narrow column visibility) but **never relax** the entity's compiled defaults.

- A workspace cannot bypass an entity's compiled `BaseFilter` (e.g., a tenant filter or a soft-delete filter from `Granit.Persistence`).
- A workspace cannot expose a column the entity hides (`IsVisible: false`).
- A workspace cannot grant a permission the user doesn't already have on the entity.

This is the **security invariant** of the navigation layer. An architecture test (planned with story #1553 in Phase 1) detects attempts to relax — e.g., a preset that removes an entity's filter clause — and fails the build.

### 5. Detail routes are workspace-agnostic

URLs split into two families:

| URL pattern | Sense |
| ----------- | ----- |
| `/w/{workspace}/{...sub}/{entity}` | Workspace-scoped collection (preset applied) |
| `/w/{workspace}/{...sub}/{entity}?peek={id}` | Side peek (Notion-style drawer) — same context |
| `/{entity}/{id}` | Canonical detail page — workspace-agnostic, sharable, indexable |

Rationale: a deep link to a customer record is a property of the customer, not of the navigation context the user happened to be in. Two users sharing `/customer/abc-123` see the same content regardless of which workspace they each landed in. Notion got this right; Granit adopts it as-is.

### 6. Permissions intersect — defense in depth

Three gates filter every workspace item at render time:

```text
visible = workspace.canRead
       AND entity.canList                          -- inherited from EntityDefinition
       AND (no workspace-item-perm OR has it)      -- optional per-item gate
```

Permissions added by this layer:

- `Workspace.{Name}.Read` — see the workspace in the navigation tree (e.g., `Workspace.Granit.Accounting.Read`).
- `Workspace.{Name}.Manage` — customize the workspace per tenant (Tier B Layer 1, ADR-040, Phase 2).

A user with access to `Granit.Accounting` but not to `Customer` sees the workspace in their sidebar — the items they can't read are filtered out (per defense-in-depth from ADR-040 / story #1549). Conversely, a user with `Customer.Read` but no access to `Granit.Accounting` reaches the customer through `Granit.Contacts` or the canonical `/customer/{id}` URL.

### 7. Per-workspace landing dashboard (optional)

A workspace may declare a landing dashboard:

```csharp
b.LandingDashboard<FinanceOverviewDashboard>();
```

When present, navigating to `/w/{workspace}` lands directly on that dashboard. When absent, the route shows the section list (sub-workspaces + items grouped by section). The landing dashboard is itself workspace-scoped — its KPIs may reference dashboard-scoped filters (per [ADR-038](./038-analytics-dashboard-definition-vs-aggregate)) and inherit the workspace's preset overlay where applicable.

### 8. Default landing route — separate concern

The 5-tier resolution of *which workspace the user lands on at login* (user-sticky > user-pinned > role-default > tenant-default > framework-default) is documented in [ADR-049](./049-default-landing-route). The role-default tier supports deep-link routes (e.g., `/w/accounting/invoicing/customers?view=overdue`), not just bare workspace names.

## Consequences

### Positive

- A module's nav surface is added by **contribution**, not by patching a workspace declared elsewhere — see [ADR-045](./045-contributor-pattern). Adding `Granit.Privacy` adds items to the framework `Privacy` workspace shell automatically; removing it removes them. No coordination required.
- Cross-workspace sharing eliminates the "should this entity live under Accounting or Contacts?" debate — it lives in both, with the right preset for each context.
- The additive-only invariant means workspace customization can never be a security regression.
- Detail-route stability (canonical `/{entity}/{id}`) makes Granit URLs first-class for sharing, bookmarking, indexing — three properties Frappe's deeply nested URLs lack.
- Permissions intersect cleanly: the same entity in two workspaces with different presets remains gated by the same `Entity.Read` permission.

### Negative / accepted trade-offs

- The depth-≤-4 cap forces flatter trees than Frappe sometimes ships. We accept this as a UX-quality decision; deeper hierarchies become tags or saved views.
- The additive-only rule means a workspace cannot offer "Customer with EXTRA fields the default doesn't show" without the entity author adding those fields to the compiled `EntityDefinition`. This is by design (Tier A ownership of the schema).
- Tenant admins (Tier B) can customize workspace layouts (Phase 2 / ADR-040 Layer 1) — reorder sections, hide items, add custom links — but cannot grant access to entities the user lacks read on. The intersection rule is unbreakable.

## Cross-references

- [ADR-040](./040-three-tier-metadata-architecture) — Three-tier metadata. Workspaces are Tier A; tenant overrides are Tier B Layer 1.
- [ADR-045](./045-contributor-pattern) — IoC contributor pattern. The mechanism modules use to add items to a workspace declared elsewhere.
- [ADR-047](./047-entity-view) — `EntityView`. User-saved views compose with workspace presets in the resolution hierarchy.
- [ADR-049](./049-default-landing-route) — Default landing route. The 5-tier resolver for "where does the user land at login".

## References

- Frappe Workspaces — research compiled in PR #1599 conversation. Hierarchical, tenant-customizable, but no cross-workspace sharing primitive (apps work around with redirects).
- Odoo Apps + record rules — research compiled in PR #1599 conversation. Cross-app sharing via record rules is powerful but the model-rule-app combinatorics get hard to debug. Granit's typed preset overlay narrows the surface.
- Notion side peek — release notes 2022-07-20. Canonical URL + drawer pattern adopted in §5 above.
- ISO 27001 A.9.4 (Access control) — the intersection rule (§6) is what makes workspace navigation auditable: the workspace is a UX primitive, not an access primitive.
