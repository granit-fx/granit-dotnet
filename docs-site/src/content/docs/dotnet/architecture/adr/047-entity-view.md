---
title: "ADR-047: EntityView supersedes Granit.QueryEngine.SavedViews"
description: "EntityView replaces the legacy filter-only SavedView with a richer first-class primitive: a basedOn delta over a compiled collection, kind inherited and immutable, three visibility levels (Personal / Shared / Tenant), three promotion flags (isPinned / isDefault / isPersonalDefault), JSONB state validated by JSON Schema per kind. Notion-style 'Save for everyone' UX. Clean break from the legacy module per Granit's pre-1.0 stance — no [Obsolete] graduation."
sidebar:
  order: 47
  label: "047 - EntityView"
---

> **Date:** 2026-04-30
> **Authors:** Jean-Francois Meyers
> **Scope:** granit-dotnet (`Granit.Entities.Views`, `Granit.Entities.Views.Endpoints`); granit-front (`@granit/entities-react` view tab strip)
> **Epic:** [#1506](https://github.com/granit-fx/granit-dotnet/issues/1506) — Refonte UI Hybride
> **Story:** [#1526](https://github.com/granit-fx/granit-dotnet/issues/1526) — ADR EntityView supersedes Granit.QueryEngine.SavedViews
> **Status:** Accepted

## Context

`Granit.QueryEngine` ships `SavedView` today: an end-user can persist a named filter + sort combo and reload it later. The shape is filter-only. With the multi-view collection model locked in [ADR-042](./042-view-catalog) (List + Kanban + Calendar + Gallery + …), and the workspace navigation locked in [ADR-044](./044-workspace-navigation) (per-workspace presets, multiple instances per kind), the legacy primitive is structurally inadequate:

- It cannot capture which **view kind** the user is looking at (a kanban view's lane field, a calendar's date field).
- It cannot capture which **columns** are visible, in which order, at which width — Notion-class power-user requirements.
- It cannot capture **per-layout config** (kanban swimlane, calendar color-by, gallery card size).
- It has no concept of **shared / tenant-default / personally-pinned** views; everything is private to the creator.
- It cannot capture **page size / density / quick filters / global search** — the things users mutate every day.

Notion ships the canonical answer to this problem: a "view" is a delta over a database, with filters / sort / group / properties / per-layout config; views can be personal (default) or shared, with explicit "Save for everyone" promotion. Linear and Airtable adopt the same shape.

Granit's planning conversation locked the synthesis: rename and rebuild as `EntityView`, model as a delta, add the visibility model, ship the Notion-style promotion UX. The pre-1.0 stance means **clean break** — no `[Obsolete]` graduation per the JF preference (memory `feedback_no_obsolete_pre_release.md`).

## Decision

### 1. Rename: `SavedView` → `EntityView`

Drop "saved" — implicit. Aligns with Notion / Linear / Airtable terminology. Files move from `src/Granit.QueryEngine/SavedViews/` to a new top-level `src/Granit.Entities.Views/` package (with `Granit.Entities.Views.Abstractions` for the lightweight contracts and `Granit.Entities.Views.Endpoints` for HTTP).

### 2. `EntityView` is a delta — `basedOn` is the foundation

A view always references a compiled collection by name:

```csharp
public sealed class EntityView : AggregateRoot
{
    Guid Id;
    string TenantId;
    string EntityName;          // e.g. "Granit.PM.Task"
    string BasedOn;             // e.g. "by-status" — name of a compiled collection (per ADR-042)
    string Kind;                // inherited from basedOn — IMMUTABLE post-creation
    string Name;                // user-facing label
    string? Description;
    string? Icon;

    JsonObject State;           // delta — validated by JSON Schema per Kind (per ADR-042 §3)

    Visibility Visibility;      // Personal | Shared | Tenant (see §4)
    Guid? OwnerId;              // null when Visibility == Tenant
    SharedWith? SharedWith;     // { Roles[], Users[] } — only when Visibility == Shared

    bool IsPinned;              // admin tab in workspace tab strip
    bool IsDefault;             // admin replaces compiled default for everyone
    bool IsPersonalDefault;     // user's landing view (per-user)

    int SortOrder;
    Audit Audit;                // who/when via Granit.Auditing
}

public enum Visibility { Personal, Shared, Tenant }
public sealed record SharedWith(IReadOnlyList<string> Roles, IReadOnlyList<Guid> Users);
```

The `State` JSON object carries the delta over `basedOn`'s compiled defaults — filters, sort, columns, group, per-layout config. Compiled changes (entity author adds a new column) propagate automatically: the view inherits the base unless `State` explicitly overrides it. Removed compiled column → graceful: state references it but the renderer ignores or warns.

### 3. `basedOn` is immutable — kind is inherited and locked

A view's `Kind` is fixed at creation by the base collection. To switch kind (kanban → calendar), the user creates a new view based on a different compiled collection. Rationale (per ADR-042 §5):

- Sanity: kanban needs a lane field; calendar needs a date field. The compiled collection's config validation is the only safe origin for those guarantees.
- Predictability: the entity author owns the catalog of kinds. Users compose deltas; they don't re-author the base.
- Schema validation: `State` is validated by the JSON Schema attached to `basedOn`'s kind. A kanban view's state cannot accidentally hold a calendar's `dateField`.

The PUT/POST validators reject any attempt to change `BasedOn` post-creation (modifying `BasedOn` would change `Kind`, invalidate `State`, and propagate confusion through every shared user's session).

### 4. Visibility model — Personal / Shared / Tenant

Three orthogonal levels, with three promotion flags:

| Visibility | Owner | Visible to | Created by |
| ---------- | ----- | ---------- | ---------- |
| **Personal** | One user | Owner only | Any user with `Entities.Views.Create` |
| **Shared** | One user | Specific roles + users (`SharedWith`) | User with `Entities.Views.Share` |
| **Tenant** | None (system) | Entire tenant | Admin with `Entities.Views.Manage` |

Three promotion flags decorate the visibility:

- `IsPinned` (admin) — view appears as a tab in the workspace's collection tab strip, alongside the compiled default.
- `IsDefault` (admin) — view replaces the compiled default for the tenant. Compiled default still loadable as `?view=__compiled__`.
- `IsPersonalDefault` (user) — view becomes the user's landing view when they navigate to the entity. Stored per-user; one per (user, entity).

The `defaultView` resolver in the manifest resolves to (in precedence order):

1. `IsPersonalDefault` — the user's pinned personal view, if any.
2. `IsDefault` — the tenant-promoted view, if any.
3. The compiled `defaultCollection` from the `EntityDefinition` — always present.

Front gets the right view at mount with no extra round-trip — see [ADR-042](./042-view-catalog) §4.

### 5. Notion-style "Save for everyone" promotion UX

When a user loads a Shared or Tenant view and modifies its filters / sort / columns:

- Modifications stay **personal** until explicit promotion. The user sees their changes; nobody else does.
- A sticky toolbar shows "**Save for everyone**" CTA — gated by view ownership (Shared) or `Entities.Views.Manage` (Tenant).
- One click promotes the local delta to the persisted view, broadcasting to every consumer at next reload.

This is the most copied UX from Notion's database/view model. It eliminates the friction of "I changed a sort and accidentally renamed the team's view." Shipped with the view tab strip in `granit-front` Phase 1.

### 6. Permissions (closed enum, defense-in-depth)

| Permission | Granted to | Operation |
| ---------- | ---------- | --------- |
| `Entities.Views.Read` | Authenticated user | List Shared views targeted to the user; list Tenant views; read own Personal views |
| `Entities.Views.Create` | Default user role | Create Personal views |
| `Entities.Views.Share` | Specific role | Promote Personal → Shared; modify own Shared views |
| `Entities.Views.Manage` | Tenant admin | Promote Shared → Tenant; pin/unpin; set tenant default |
| `Entities.Views.Delete.Any` | Tenant admin | Delete any view (moderation) |

All write operations are **audited via `Granit.Auditing`** — who created/modified/promoted/deleted, when, the before/after `State` diff. ISO 27001-friendly.

### 7. Migration from legacy `SavedView` — clean break, no `[Obsolete]`

Granit is pre-1.0 (memory `feedback_no_obsolete_pre_release.md`). The migration ships in two PRs (the second already plotted in story #1546):

- **PR 1**: introduce `Granit.Entities.Views` + `Granit.Entities.Views.Endpoints` (Phase 1). Legacy `Granit.QueryEngine.SavedViews` cohabits.
- **PR 2**: every consuming app's data migrates via an EF migration (`SavedView` rows → `EntityView { Kind: "list", BasedOn: "default", State: { Filters, Sort } }`). The legacy module is removed in the **same PR** that consumes the migration. No `[Obsolete]` graduation; documentation references rewritten.

The architecture test from #1605 (`AbstractionsPurityTests`) ensures `Granit.Entities.Views.Abstractions` stays runtime-free, consistent with the pattern established in PR #1603.

### 8. JSONB `State` schema — validated per kind

The `State` payload is a JSONB column (PostgreSQL native; SQLite mock for tests). Validation uses the JSON Schema generated from the compiled collection's per-kind config (per ADR-042 §3). Examples:

- A `kanban` view's state may include `{ filters: [...], sort: [...], laneField: "Status", swimlanes: "Priority" }` — schema rejects `dateField` (not part of the kanban kind).
- A `calendar` view's state may include `{ filters: [...], dateField: "DueDate", colorBy: "Priority" }` — schema rejects `laneField`.

Validation runs on PUT/POST; persisted views are guaranteed to round-trip cleanly into the renderer.

## Consequences

### Positive

- One primitive covers the full Notion-class power-user surface: kind selection, columns, sort, filters, group, per-layout config, sharing.
- The delta-over-base shape means compiled changes propagate automatically — entity authors aren't blocked by N persisted views per tenant.
- Visibility + promotion model captures the reality of admin-managed defaults (Tenant), team-shared workflows (Shared), and personal preference (Personal) without code branching.
- The Notion-style "Save for everyone" UX eliminates a known UX trap (accidentally promoting personal changes to a shared view).
- Clean break from `SavedView` keeps the codebase honest pre-1.0 — no graduated deprecation cruft.

### Negative / accepted trade-offs

- One additional EF migration per consuming app at the migration cutover. Mitigated by the documented script in #1546.
- Tenant admins cannot author a view that crosses kinds (e.g., a "kanban-or-calendar" hybrid). This is the right boundary — an `EntityDefinition` author can ship two collections (`Kanban("by-status")` + `Calendar("deadlines")`), and users save deltas of each independently.
- Removed compiled fields surface as dangling references in old views. Mitigation: a `customization.warnings: ["field 'Foo' no longer exists"]` array on the manifest, surfaced as a banner in the view tab strip — the user re-saves to clean up.

## Cross-references

- [ADR-040](./040-three-tier-metadata-architecture) — Three-tier metadata. `EntityView` is a per-user runtime delta on Tier A (compiled collection); sits between Tier A and Tier B Layer 1 in the resolution hierarchy.
- [ADR-042](./042-view-catalog) — View catalog. `BasedOn` references one of these compiled collections; `Kind` is inherited; `State` is validated by the per-kind JSON Schema.
- [ADR-044](./044-workspace-navigation) — Workspace navigation. Pinned views show up in the workspace's tab strip; tenant-default views are picked up by `defaultView` resolver alongside the compiled fallback.
- Memory `feedback_no_obsolete_pre_release.md` — Granit is pre-1.0; clean breaking changes preferred over `[Obsolete]`.

## References

- Notion database / view separation — adopted as-is. The "Save for everyone" promotion UX is the most copied gesture across modern productivity tools.
- Linear views — same delta-over-base shape; Linear adds query templates we don't (yet) need.
- Airtable views — adds calendar / gallery / Gantt as first-class kinds; Granit's catalog mirrors this (per ADR-042).
- Existing `Granit.QueryEngine.SavedViews` — filter-only, the scope was always too narrow; the rename + rebuild is the right move.
