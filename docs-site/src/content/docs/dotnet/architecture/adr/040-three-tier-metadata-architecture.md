---
title: "ADR-040: Three-tier metadata architecture"
description: "Granit splits its declarative metadata into three orthogonal tiers: compiled (versioned in Git, the framework's identity), tenant customization (runtime layout overrides only — no schema change), and Niveau B custom fields (JSONB extension table, deferred to Phase 4). Tier A never depends on Tier B; business logic operates exclusively on the compiled schema."
sidebar:
  order: 40
  label: "040 - Three-Tier Metadata Architecture"
---

> **Date:** 2026-04-30
> **Authors:** Jean-Francois Meyers
> **Scope:** granit-dotnet (`Granit.Entities`, `Granit.Entities.Customization`, future `Granit.Entities.Customization.Fields`)
> **Epic:** [#1506](https://github.com/granit-fx/granit-dotnet/issues/1506) — Refonte UI Hybride
> **Story:** [#1520](https://github.com/granit-fx/granit-dotnet/issues/1520) — ADR-040 Three-tier metadata architecture
> **Status:** Accepted

## Context

The Refonte UI Hybride initiative ([#1506](https://github.com/granit-fx/granit-dotnet/issues/1506)) introduces three new declarative primitives — `EntityDefinition<T>`, `EntityView`, `WorkspaceDefinition` — that let a React shell render auto-generated CRUD screens from a server-side metadata manifest. Before any code lands, we need to lock the **boundary between what's compiled, what's runtime-configurable, and what's deliberately out of scope**.

Two reference frameworks shape this conversation:

- **Frappe / ERPNext** ships with a stunning UX dividend: one DocType declaration drives schema + UI + API + permissions, end users add custom fields through the admin UI, and tenant admins drag-and-drop forms in real time. The cost is severe: schema mutates from the UI (`ALTER TABLE` at runtime), customizations are rows in `tabCustomField` / `tabPropertySetter`, and the boundary between "safe" and "dangerous" customization (Server Scripts, Client Scripts) is invisible until production. ERPNext's own community discussions identify the resulting fragility — migration drift, type erosion, opaque customization layers — as the framework's largest pain point.
- **Notion** keeps the user-facing flexibility but couples it to a fully dynamic schema. Power users hit a cliff past ~5 000 rows because every customization is data, not code; there's no compile-time invariant to lean on.

Granit's existing identity is the inverse: **code-first, type-safe, isolated DbContexts, deterministic EF migrations**. We want the UX dividend (one declaration, many surfaces) without the schema-mutation tax. That requires drawing a sharp line between layers.

## Decision

Granit's metadata splits into **three orthogonal tiers**. The framework's tronc commun ships Tiers A and B; Tier C is explicitly out of scope.

### Tier A — Compiled schema

The framework's identity. Versioned in Git. Deterministic. ISO 27001-friendly.

- **Source of truth:** EF Core entities + `EntityDefinition<T>` / `EntityView` / `WorkspaceDefinition` declared in C#.
- **Lifecycle:** evolved through pull requests, reviewed, deployed atomically.
- **Schema changes:** only via deterministic EF Core migrations checked into Git, generated and reviewed by developers, applied at deploy time. **Never at runtime.**
- **Owns all logic:** validations, workflows, computations, permissions, business rules. Logic only ever reads Tier A.
- **JSON manifest:** generated from Tier A at request time (after permission filtering); never authored by hand.

This is where the framework's "Solid by design" reputation lives.

### Tier B — Tenant customization

Tenant admins reshape the **presentation** of compiled artifacts at runtime — they cannot alter the schema, the validations, or the logic.

- **Layer 1 (Phase 2 of #1506):** form layout overrides — reorder, regroup, hide compiled fields; reorder workspace sections; add custom workspace items (links, sub-workspaces). Stored in `Granit.Entities.Customization`. Audited via `Granit.Auditing`. Closed enum of mutable properties (`Hidden`, `Section`, `Order`, `Width`, `Label`) — never `Type`, never `Required`, never `RequiredPermission`, never `Validation`.
- **Layer 2 (Phase 4 — Niveau B):** custom fields per tenant, stored in a typed JSONB extension table keyed by `(tenantId, entityName, entityId, fieldKey)`. Module: `Granit.Entities.Customization.Fields`. Strict guarantees: never collides with a compiled field name; validation surfaces only via JSON Schema generated from the field definition; never overrides compiled validation, permission, or widget; **no `ALTER TABLE` at runtime, ever**.

Both layers are **strictly additive** — they enrich the manifest emitted by Tier A; they cannot remove a compiled `Required`, broaden a compiled permission, or short-circuit a compiled validation. The defense-in-depth contract is preserved.

### Tier C — Dynamic resources

End users define brand-new entity shapes from the admin UI, with new tables created on the fly (Frappe Custom DocTypes, Airtable bases, Notion databases).

**Out of scope for Granit's tronc commun.** A separate optional module `Granit.LowCode` may revisit this one day — it would necessarily live outside the type-safe / ISO 27001 / deterministic-migration story. Not folded into the trunk.

## The Golden Rule (non-negotiable)

> **Tier A code never depends on Tier B data.**
> **All business logic — validations, workflows, computations, permissions, audit invariants — operates exclusively on the compiled schema.**

Concrete consequences:

- A FluentValidation validator never reads from the JSONB extension table.
- A workflow transition never branches on a tenant custom field.
- An audit policy never references a layer-1 layout override.
- A metric / dashboard / export / OData filter is always defined over compiled columns.

Tier B is **purely presentation + storage**: the manifest emits "here's a tenant-added field, render it as text"; the value round-trips through JSONB on save; nothing else in the framework reads it. This is the structural guarantee that Granit isn't Frappe.

## Resolution hierarchy

When the React shell loads a screen, the manifest is composed top-down. The compiled defaults always seed the document; each runtime layer is a delta on what's already there. Each layer can constrain or annotate; none can relax a Tier A invariant.

```text
1. URL state             (ad-hoc filters typed by the user)
2. Saved EntityView      (Personal > Shared > Tenant promotion order)
3. Workspace preset      (additive only — can constrain, never relax)
4. Tenant customization  (Layer 1 form / workspace layout overrides — Phase 2)
5. Compiled defaults     (EntityDefinition + EntityView + WorkspaceDefinition — Tier A)
```

The defense-in-depth filter (per ADR-044 — IoC contributor pattern, and the upcoming Phase 1 `Granit.Entities.Endpoints` story #1549) runs **after** the merge and **before** the response leaves the server. A field the user lacks `RequiresPermission` for is absent from the payload — never just hidden.

## Consequences

### Positive

- The framework keeps its "code-first, type-safe, deterministic" identity intact while delivering Frappe-class auto-generated CRUD UX.
- ISO 27001 / GDPR audit story remains clean: every schema change is reviewable in Git; every layout customization is audited per tenant.
- Migrations stay deterministic and live in the consuming app (per the existing CLAUDE.md rule: framework packages MUST NOT contain EF Core migrations).
- The "field inspector" debug overlay (planned in Phase 3) can authoritatively answer "which tier hid this field?" — eliminating Frappe's worst debugging trap.

### Negative / accepted trade-offs

- Tenant admins cannot add a custom *required* field, custom validation, or custom workflow logic. They get presentation customization (rich) and custom fields with declarative validation (Phase 4). For everything else, they file a feature request that becomes a Tier A change.
- Adding a new admin-visible field is a developer task, not an admin task. This is the correct boundary for ISO 27001 / SOX-grade applications; it's the wrong boundary for unstructured personal-productivity tools (which is why Notion / Airtable target a different market).

### Anti-Frappe / anti-Odoo guarantees

These are direct inversions of pain points documented in our research (see [`PR #1599`](https://github.com/granit-fx/granit-dotnet/pull/1599) commentary and the conversation underlying #1506):

1. **No script in DB.** Period. There is no `ir.actions.server.code` analog and there will never be one. Tenant logic, when needed, ships as compiled C# in an opt-in module.
2. **No runtime DDL.** Layer 1 reorders; Layer 2 uses a typed extension table; the schema never mutates from a UI gesture.
3. **One declaration, multiple views.** Steal Frappe / Notion's "one source, many renders" insight. The compiled `EntityDefinition` drives list, kanban, calendar, gallery, form, detail, dashboard, smart-button relations — all from one C# file.
4. **Typed C# manifests at the source.** The JSON manifest is generated; never authored. Refactoring is `rename` in an IDE, not grep across DB rows.
5. **Closed enums for mutable properties.** Tier B's mutable surface is a closed `enum` of layout-shaped knobs — never `eval:` strings, never arbitrary code paths.
6. **Field inspector overlay** (Phase 3, dev UI) — show which tier and which permission rule hid a field. This single tool would have eliminated half of Frappe's developer-experience complaints.

## Cross-references

- [ADR-041](./041-widget-catalog) — Widget catalog and naming convention (the visual primitives Tier A declares; consumed by all three tiers' renderers).
- [ADR-042](./042-view-catalog) — View catalog (List / Kanban / Calendar / Gallery / …) for collections, declared at Tier A; user views (Tier-A-style `EntityView` deltas) layer on top.
- [ADR-046](./046-entity-view) — `EntityView` supersedes `Granit.QueryEngine.SavedViews`; Personal / Shared / Tenant promotion model is a clean Layer 1.5 (per-user runtime delta) that sits between Tier A and Tier B without compromising the Golden Rule.
- [ADR-044](./044-contributor-pattern) — Inversion-of-control contributors; how cross-module modules graft onto a Tier A entity without coupling.

## References

- Frappe Customize Form anatomy and pain points — research compiled in PR #1599 conversation. Highlights: `tabPropertySetter` row-as-column-override design is brilliant for ergonomics, devastating for type safety; Server / Client Scripts disabled by default in v15 is an admission of failure.
- Notion database / view separation — `database = schema, view = projection` mental model adopted as-is. Notion's "Save for everyone" promotion UX is replicated by the EntityView visibility model (ADR-046).
- ISO 27001 A.12.1.2 (Change management) — the boundary between Tier A and Tier B is precisely what enables compliance: compiled schema = reviewable change history; tenant overrides = audited runtime data.
