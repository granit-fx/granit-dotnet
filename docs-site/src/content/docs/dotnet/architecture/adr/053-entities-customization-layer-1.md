---
title: "ADR-053: Granit.Entities.Customization — Layer 1 tenant overrides"
description: "Granit introduces a Layer 1 customization model that lets tenant administrators reorder, regroup, and hide compiled fields on any EntityDefinition without touching code. The vocabulary is closed (reorder / regroup / hide; never add) and resolves through a 5-layer hierarchy beneath URL state, saved views, and workspace presets. Every override is captured by ISO 27001 audit, gated by an RBAC permission, and surfaced in the runtime manifest with per-field provenance so a dev-mode field inspector can attribute every rendered field to its source layer."
sidebar:
  order: 53
  label: "053 - Layer 1 Customization"
---

> **Date:** 2026-05-02
> **Authors:** Jean-Francois Meyers
> **Scope:** NEW modules `Granit.Entities.Customization`, `Granit.Entities.Customization.EntityFrameworkCore`, `Granit.Entities.Customization.Endpoints`. Touches `Granit.Entities.Endpoints` (manifest composer integration in B4).
> **Epic:** [#1510](https://github.com/granit-fx/granit-dotnet/issues/1510) — Phase 2 (Activities + Customization L1 + Reactions + Relations refinement)
> **Stories:** [#1806](https://github.com/granit-fx/granit-dotnet/issues/1806) (this ADR), [#1807](https://github.com/granit-fx/granit-dotnet/issues/1807) (B1 core), [#1808](https://github.com/granit-fx/granit-dotnet/issues/1808) (B2 EF), [#1809](https://github.com/granit-fx/granit-dotnet/issues/1809) (B3 endpoints), [#1810](https://github.com/granit-fx/granit-dotnet/issues/1810) (B4 manifest composer)
> **Status:** Accepted

## Context

Granit's entity surface is **compiled-first**: every form, list, calendar, gallery, and detail layout is declared in C# via `EntityDefinition<T>` and surfaces to the React shell through the runtime manifest endpoint (`GET /api/entities/{name}/manifest`, see [ADR-040](./040-three-tier-metadata.md) for the three-tier metadata architecture). Compiled-first gives the framework type-safety, refactor-safety, and IDE-aided discoverability — at the cost of zero per-tenant flexibility out of the box.

Real deployments need at least three flavours of customization:

1. **Cosmetic re-ordering / hiding** — the tenant accountant wants the *Currency* field next to *Total* on the invoice form, and wants the *Internal notes* group hidden from the sales role. Compile-time changes are out of scope (one tenant cannot fork the core).
2. **Saved filtered views** — already partially covered by [ADR-047 (Entity View)](./047-entity-view.md): personal / shared / tenant-level saved query/filter combinations on list, calendar, and gallery layouts.
3. **Custom fields** — adding a *Vehicle plate* field to `Party` because the tenant runs a fleet-management vertical. **This is not in scope here**; it is the Layer 2 problem and is deferred to Phase 4 (see §[Out of scope](#out-of-scope)).

This ADR locks the shape of category 1 — call it **Layer 1 customization** — and the resolution hierarchy that integrates it with categories 2 (saved views), URL state, and workspace presets ([ADR-044](./044-workspace-navigation.md)).

The competitive landscape shapes the design space:

- **Odoo Studio** ships *Studio* on top of `ir.ui.view` with XPath inheritance and per-tenant view records. It is unconstrained — XPath lets a tenant rewrite anything, including breaking field types — and the result is the legendary "studio nightmare" upgrades. Granit refuses this.
- **Frappe / ERPNext** has *Customize Form* — a closed delta vocabulary applied to compiled DocType definitions. Closer to Granit's target, but the field inspector tooling is bolted on (developer mode toggles a debug overlay) and the audit trail of "who hid what when" is weak. Granit can do better with manifest provenance shipped from day one.
- **Salesforce** Page Layouts + Lightning App Builder give the most polished result through massive vendor investment. Out of reach for a one-person framework — but the *closed-vocabulary delta* idea travels.

Phase 1.5 already shipped the precondition: the manifest payload carries the full list of fields per layout (`form-default`, `detail-default`, list `QueryDefinition`, `CalendarLayoutDescriptor`, `GalleryLayoutDescriptor`). Layer 1 customization is the layer that mutates that payload server-side, per tenant, before it ships to the React shell.

## Decision drivers

- **Anti-Studio ergonomics.** Layer 1 must be a closed, validated vocabulary. A tenant must not be able to rewrite a field type, change a permission, or reorder a workflow transition through the customization UI.
- **Anti-Frappe field-inspector pain.** The runtime manifest must carry per-field `provenance` so a dev-mode overlay can answer "where does this field come from?" without grepping the codebase. Treat developer experience as a first-class deliverable, not a debug toggle.
- **ISO 27001 audit.** Every customization mutation is a tenant-administrator action that affects what other users see; it must hit `IAuditLogService.LogAsync` with old + new delta. Compliance, not nice-to-have.
- **RBAC gate.** The customization endpoints sit behind a single permission so tenant admins can delegate without granting platform admin.
- **Phase scoping.** Layer 1 is shippable in 5 stories with a tight blast radius (one new module family + manifest composer touch). Layer 2 (custom fields) needs migration runtime, query-engine plumbing, and per-tenant DDL — out of scope for Phase 2.
- **Per-tenant, never per-user.** Per-user UI overrides multiply the resolution hierarchy permutations (per-user × per-role × per-tenant) and are already covered functionally by saved Entity Views (ADR-047). Phase 2 ships per-tenant only.

## Considered alternatives

### A — Free-form JSONB layout per tenant

Store the full layout as a tenant-owned JSON document. Tenants edit the document; the manifest composer returns it as-is.

- **Pros**: maximum flexibility; no closed vocabulary to design.
- **Cons**: no validation against the compiled definition (a typo in a field name silently drops the field from the layout). No per-layer attribution (if `firstName` is in position 3, was it the compiled default or a tenant override?). No audit-friendly diff (the JSON document mutates as a blob; auditors see "before / after" but cannot reconstruct the user intent). Re-introduces the Odoo Studio failure mode at the JSON layer.
- **Rejected**.

### B — Closed delta vocabulary (CHOSEN — see §[The closed delta vocabulary](#the-closed-delta-vocabulary))

A small, fixed set of operations (`Reorder`, `Regroup`, `Hide`) applied as deltas on top of the compiled definition. Each delta references compiled fields by name; unknown names are rejected at write time.

- **Pros**: validation is straightforward (a field name either exists in the compiled definition or it does not). Per-layer attribution falls out for free — every rendered field carries its delta's id (or "compiled" if untouched). Audit trail captures user intent (`{ op: "Hide", fieldName: "InternalNotes" }`) instead of opaque JSON diffs. Refuses any "add a field" attempt — the vocabulary does not include it.
- **Cons**: bounded expressiveness. Tenants who want richer customization wait for Layer 2.
- **Selected**.

### C — Compile-time customization via `IConfigureOptions<EntityDefinition<T>>`

Apply customization at module composition by registering an `IConfigureOptions<EntityDefinition<T>>` per tenant.

- **Pros**: zero runtime cost; uses an existing .NET pattern.
- **Cons**: `IConfigureOptions` runs once at container build, not per request. Tenant admins cannot mutate at runtime without a process restart. Disqualifies the use case immediately.
- **Rejected**.

## Decision

### The closed delta vocabulary

Three operations, exhaustively. Anything else is Layer 2 and is rejected at write time:

```csharp
public abstract record CustomizationDelta(string FieldName);

/// <summary>Move a field to a new position within its current group.</summary>
public sealed record Reorder(string FieldName, string? BeforeFieldName, string? AfterFieldName) : CustomizationDelta(FieldName);

/// <summary>Move a field into a different declared group.</summary>
public sealed record Regroup(string FieldName, string GroupKey) : CustomizationDelta(FieldName);

/// <summary>Drop a field from the rendered layout. The compiled definition's permission gate is unchanged.</summary>
public sealed record Hide(string FieldName) : CustomizationDelta(FieldName);
```

Validation rules at write time, enforced by the B3 endpoints layer before persistence:

- `FieldName` MUST resolve in the compiled `EntityDefinition<T>`. Unknown names → `400 Bad Request` with the offending name in `Problem.Detail`.
- `Reorder` MUST set exactly one of `BeforeFieldName` / `AfterFieldName`; both null or both set → `400`.
- `Regroup.GroupKey` MUST resolve in the compiled definition's group catalogue.
- `Hide` MUST NOT target a field whose compiled metadata is `IsRequired = true` and is the entity's primary key — the manifest still has to carry an identifier. Other required fields can be hidden; the form layer surfaces a server-side validation error if the user attempts to save without a value.
- Field permissions ([ADR-040](./040-three-tier-metadata.md) §3) are **not** mutated — `Hide` removes the field from the rendered layout but the underlying authorization gate is untouched. A tenant cannot grant a permission this way.

#### Never `Add`

The conspicuous omission is `Add`. Layer 1 cannot introduce a new field because:

- It would need a column in the underlying entity (the compiled aggregate).
- It would need a `QueryDefinition` column to be filterable and a `ExportDefinition` row to be exportable.
- It would need its own permission, validation, and EF Core configuration.

All four are compile-time concerns. Pretending they are runtime concerns is exactly the move that turns customization into the legendary upgrade-breaking studio fork. Layer 2 ships the proper machinery in Phase 4 (see [Out of scope](#out-of-scope)).

### The 5-layer resolution hierarchy

The manifest endpoint resolves the layout by walking five layers, highest priority first. Each layer can override the lower ones; layers below contribute the fields the upper layers do not touch.

| Priority | Layer | Source | Scope | Mutable at runtime? |
| -------- | ----- | ------ | ----- | ------------------- |
| 1 | URL state | Query string (`?sort=…&filter=…`) | Per request | Yes — every request |
| 2 | Saved Entity View | `Granit.Entities.Views` ([ADR-047](./047-entity-view.md)) | Personal > Shared > Tenant | Yes — by user (CRUD) |
| 3 | Workspace preset | Workspace `*Page.SetDefault*` calls ([ADR-044](./044-workspace-navigation.md)) | Per workspace, additive only | Yes — by workspace admin |
| 4 | **Tenant customization (Layer 1) — this ADR** | `Granit.Entities.Customization` aggregates | Per tenant, per layout | Yes — by tenant admin via PUT |
| 5 | Compiled defaults | C# `EntityDefinition<T>` | Global | No — code change |

Read sequence (manifest composer):

1. Pull the compiled `EntityDefinitionDescriptor` (layer 5).
2. Apply tenant customization deltas in declaration order, mutating the descriptor's column / group lists in-place (layer 4).
3. Apply workspace preset additions if the request carries a workspace context (layer 3). Layer 3 is **additive only** — it can pre-fill default sort, default page size, default filter chips; it cannot reorder or hide.
4. Apply saved Entity View overrides if the request carries `?view={id}` (layer 2).
5. Apply URL state if present (layer 1).

A field that is hidden by Layer 4 cannot be unhidden by Layer 3 or higher — Layer 4 is the floor for whether a field appears at all. Layers 1 / 2 can re-sort or filter the visible set but cannot resurrect a hidden field. (Tenant admins explicitly choose to hide; users cannot bypass that.)

### Manifest provenance

Every field in the manifest payload carries a `provenance` token after composition:

```jsonc
{
  "name": "currency",
  "label": "Currency",
  "type": "string",
  "provenance": {
    "layer": "compiled",                  // or "tenant-customization", "workspace-preset", "saved-view", "url-state"
    "overrideId": null                    // GUID when layer is tenant-customization or saved-view
  }
}
```

The dev-mode field inspector overlay (React shell, behind a `?debug=manifest` flag or a developer toolbar toggle) renders each field with a small badge attributing its source layer; clicking the badge opens the override (when `overrideId` is set) or jumps to the C# source location (compiled). This kills the "where does this field come from?" forensic question that haunted Frappe deployments.

### Audit trail

Every `PUT /api/entities/{name}/customization` (B3) calls `IAuditLogService.LogAsync` with the (old, new) delta set:

```csharp
await audit.LogAsync(new AuditEntry(
    action: "Granit.Entities.Customization.Updated",
    targetType: $"Granit.Entities.Customization:{entityName}",
    targetId: customization.Id,
    oldValue: JsonSerializer.Serialize(previousDeltas),
    newValue: JsonSerializer.Serialize(newDeltas),
    userId: currentUser.Id,
    tenantId: currentTenant.Id), cancellationToken);
```

Audit captures user intent (the delta vocabulary is human-readable) rather than opaque JSON diffs. Auditors and tenant admins can replay the customization history without consulting a developer.

### Permission gate

Single permission, [`Group.Resource.Action`](../../../CLAUDE.md#permissions--naming-convention-strict) format:

- `Entities.Customization.Manage` — read + write the tenant's customization for any entity. Required on the B3 PUT endpoint.

No per-entity permission is added — the customization is a tenant-administration responsibility, not a per-entity one. Tenant admins who can customize one entity can customize all of them; entities they cannot read still resolve via the read-side permission on the manifest endpoint, so customization writes against an unreadable entity are blocked at the manifest read step that follows.

### Storage shape

Persisted via `Granit.Entities.Customization.EntityFrameworkCore` (B2). One row per (tenant, entity name), JSONB column carrying the delta list:

```sql
CREATE TABLE entity_customizations (
    id UUID PRIMARY KEY,
    tenant_id UUID NOT NULL,
    entity_name VARCHAR(256) NOT NULL,
    layout_kind VARCHAR(64) NOT NULL,    -- "form-default", "detail-default", "list", "calendar", "gallery"
    deltas JSONB NOT NULL,
    created_at TIMESTAMPTZ NOT NULL,
    created_by VARCHAR(256) NOT NULL,
    modified_at TIMESTAMPTZ,
    modified_by VARCHAR(256),
    CONSTRAINT uq_tenant_entity_layout UNIQUE (tenant_id, entity_name, layout_kind)
);
```

The unique constraint guarantees one customization per (tenant, entity, layout) — the PUT endpoint is a full replace, not a delta append. Deletion is hard delete; revert-to-default is achieved by deleting the row.

### FusionCache integration

The manifest composer caches the resolved per-tenant payload for hot reads. Layer 1 mutations invalidate the cache via tag:

- Cache key: `entity-manifest:{tenantId}:{entityName}` (already in place from Phase 1.5).
- Tag: `entity-customization:{tenantId}:{entityName}` — emitted by the PUT endpoint after a successful write.
- Wolverine handler `EntityCustomizationCacheInvalidator` consumes `EntityCustomizationUpdatedEto` and calls `IFusionCache.RemoveByTagAsync(tag)`.

Same pattern as relation aggregate invalidation ([ADR-048](./048-cross-module-entity-relations.md) — `RelationAggregateCacheInvalidator`).

## Consequences

### Positive

- Tenant admins gain real, validated customization without a code change.
- Audit trail is human-readable (delta vocabulary) and ISO 27001-compatible by default.
- Manifest provenance kills the "where does this field come from?" forensic question and lowers onboarding cost for developers joining a customized tenant.
- Closed vocabulary refuses by design the upgrade-breaking moves that haunt Odoo Studio deployments.
- Phase 4 Layer 2 (custom fields) inherits a clean foundation: it adds an `Add` operation behind its own permission and a separate persistence layer for the new column metadata.

### Negative

- Bounded expressiveness — tenants asking for richer customization (conditional visibility, per-role overrides, formula-driven defaults) wait for Phase 4 or beyond.
- One additional cache invalidation handler per tenant write — the FusionCache tag pattern keeps this O(1) but it is one more piece of plumbing to maintain.
- The "never `Add`" rule will be tested by the first vertical-app prospect that needs a custom field. Resist; document Layer 2's roadmap.

### Neutral

- Per-tenant only in Phase 2. Per-user UI overrides remain functionally available through saved Entity Views ([ADR-047](./047-entity-view.md)) and are not on the Layer 1 roadmap.
- Workspace presets ([ADR-044](./044-workspace-navigation.md)) remain additive-only — Layer 1 owns the structural overrides.

## Out of scope

- **Layer 2 — custom fields.** Adding new columns to the underlying aggregate, with their own permissions, validation, query whitelisting, export whitelisting, and EF Core configuration. Phase 4 initiative.
- **Per-user UI overrides.** Use saved Entity Views ([ADR-047](./047-entity-view.md)) for personal layouts; no per-user row in `entity_customizations`.
- **Conditional visibility / formula defaults.** Out of vocabulary in Layer 1; revisit in a follow-up ADR if Phase 4 picks them up.
- **Cross-tenant template customization.** A tenant cannot publish their customization for another tenant to inherit; the platform admin can ship vertical-app defaults at compile time only.

## References

- [ADR-040 — Three-tier metadata architecture](./040-three-tier-metadata.md)
- [ADR-044 — Workspace navigation](./044-workspace-navigation.md)
- [ADR-047 — Entity View (saved query/filter combinations)](./047-entity-view.md)
- [ADR-048 — Cross-module entity relations](./048-cross-module-entity-relations.md)
- [Phase 2 epic #1510](https://github.com/granit-fx/granit-dotnet/issues/1510)
- [Master plan #1506 §Resolution hierarchy](https://github.com/granit-fx/granit-dotnet/issues/1506)
