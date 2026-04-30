---
title: "ADR-042: View catalog and per-kind config schema"
description: "Granit's collection facet exposes a closed catalog of view kinds (List, Kanban, Calendar, Gallery, plus Timeline/Map/Tree/Gantt on demand). Each kind has a typed configuration schema validated at startup. The same closed-set + namespaced-extension pattern as ADR-041 applies to view kinds. Saved views (EntityView) are bound to the kind of their base compiled collection — switching kind requires saving from a different collection."
sidebar:
  order: 42
  label: "042 - View Catalog"
---

> **Date:** 2026-04-30
> **Authors:** Jean-Francois Meyers
> **Scope:** granit-dotnet (`Granit.Entities` collections facet); granit-front (`@granit/entities-react` view renderers)
> **Epic:** [#1506](https://github.com/granit-fx/granit-dotnet/issues/1506) — Refonte UI Hybride
> **Story:** [#1522](https://github.com/granit-fx/granit-dotnet/issues/1522) — ADR-042 View catalog
> **Status:** Accepted

## Context

The Notion-style mental model locked during planning splits a collection of records into two concerns:

- **Data source** — what to query, with what filters / sort / grouping. Already covered by `Granit.QueryEngine.QueryDefinition<T>`.
- **View** — how to render the result set. List, Kanban, Calendar, Gallery, Timeline, Map, Tree, Gantt — same database, multiple presentations.

Frappe ships ~10 view types (List, Report, Kanban, Calendar, Gantt, Image, Tree, Map, Dashboard) with config conventions per type but no formal catalog or schema. The result: each app reinvents the binding (Calendar needs a `start_date`; Kanban needs a `Select` field; Tree needs `is_tree: 1`); errors surface at user click time, not at developer boot time.

Granit needs the same multi-view dividend with the type-safety it gets from compile-time enums and JSON Schema validation. ADR-041 (widget catalog) established the pattern for fields; this ADR extends it to view kinds.

## Decision

### 1. Standard view-kind catalog (closed set, framework-owned)

The framework ships a fixed catalog of **view kinds**. Every kind has:

- A canonical name (string, lowercase, kebab-case for multi-word — none today are multi-word).
- A typed configuration schema (validated at startup).
- A reference React renderer in `@granit/entities-react`.
- A documented data-source contract (what shape the QueryDefinition must expose).

| Kind | Phase | Renderer | Required config | Optional config | Data-source contract |
| ---- | ----- | -------- | --------------- | --------------- | -------------------- |
| `list` | 1 | `<EntityList />` table | — | `columns[]` (visible/order/width), `density` (compact/normal/comfortable), `defaultSort` | Any `QueryDefinition` |
| `kanban` | 1 | `<EntityKanban />` board | `laneField` (must be `select`/`status`/lookup) | `cardFields[]`, `swimlanes` (second group-by), `wipLimits` | QueryDefinition exposes the lane field as filterable + groupable |
| `calendar` | 1.5 | `<EntityCalendar />` month/week | `startField` (must be `date`/`datetime`) | `endField`, `titleField`, `colorBy` | QueryDefinition exposes a date field; range-query endpoint required (per Phase 1.5 story) |
| `gallery` | 1.5 | `<EntityGallery />` cards | `imageField` (must be `file`/`image` BlobStorage ref) | `titleField`, `subtitleField`, `cardSize` (s/m/l) | QueryDefinition exposes the image field |
| `timeline` | 3 | `<EntityTimeline />` horizontal | `startField`, `endField` | `groupBy`, `colorBy` | QueryDefinition exposes start/end date fields |
| `map` | 3 (on demand) | `<EntityMap />` geo | `latField`, `lngField` (or `geoField` for PostGIS) | `colorBy`, `clusterAt` | QueryDefinition exposes lat/lng or geography |
| `tree` | 3 (on demand) | `<EntityTree />` hierarchy | `parentField` (self-reference) | `expandDefault` | QueryDefinition exposes the parent reference |
| `gantt` | 3 (on demand) | `<EntityGantt />` schedule | `startField`, `endField` | `dependenciesField`, `progressField` | QueryDefinition exposes start/end + optional dependencies |

This is the **v1 catalog**. Adding a new kind requires an ADR amendment, a renderer in `@granit/entities-react`, and coordinated wiring in `Granit.Entities.Endpoints`. Removing a kind is a breaking change.

### 2. Custom view-kind namespacing — `custom:<app-prefix>-<name>`

When an app needs a view that isn't in the standard catalog (e.g., a sales pipeline with custom drag-and-drop semantics, an org chart, a flow diagram), the backend declares `custom:<app-prefix>-<name>` and the front-end registers a matching renderer in its `customViewRegistry`. Same rules as the widget catalog (ADR-041 §2):

- `custom:` prefix mandatory; bare unknown names rejected at boot via the integrity check.
- `<app-prefix>` matches `^[a-z][a-z0-9]*$`, identifies the owning module / app.
- `<name>` matches `^[a-z][a-z0-9-]*$` (kebab-case).
- Frontend `customViewRegistry` is a typed TypeScript map; missing-key access is a compile-time error in the showcase app.
- Manifest passes the config opaquely; the front's typed registry validates it.

### 3. Per-kind config schema, validated at startup

Each standard view kind ships with a JSON Schema (in `Granit.Entities.Abstractions/Views/`) that the boot-time integrity check validates. A `kanban` view without a `laneField` fails at app start, not at user click time. The fluent C# builder exposes typed wrappers that make the invalid state unrepresentable:

```csharp
b.Collections(c => c
    .List("default")                                         // no required config
    .Kanban("by-status", k => k
        .LaneField(t => t.Status)                            // required, typed
        .CardField(t => t.Title)
        .CardField(t => t.AssignedTo)
        .CardCover(t => t.CoverImageBlobRef))                // optional
    .Calendar("deadlines", cal => cal
        .StartField(t => t.DueDate)                          // required, typed
        .TitleField(t => t.Title)));
```

The builder rejects invalid combinations at compile time (you cannot pass a `string` field to `.LaneField(...)` if the typed enum requires `select`/`status`/lookup).

### 4. Multiple instances per kind — named, addressable

A single `EntityDefinition` can declare multiple collections of the same kind:

```csharp
b.Collections(c => c
    .List("default")
    .List("compact")                                         // second list, denser columns
    .Kanban("by-status",   k => k.LaneField(t => t.Status))
    .Kanban("by-assignee", k => k.LaneField(t => t.Assignee)));
```

Each is addressable by `(kind, name)` from the front: `<EntityList name="task" view="compact" />`, `<EntityKanban name="task" view="by-assignee" />`. Names are unique per `(entity, kind)`.

### 5. Saved views are bound to a compiled collection's kind

Per [ADR-047](./047-entity-view), a saved `EntityView` carries a `basedOn` reference to a compiled collection (e.g., `basedOn: "by-status"`). The view's `kind` is **inherited from the base collection and immutable**. To switch kind, the user creates a new view based on a different compiled collection.

This rule has two consequences:

- The catalog is the entity author's choice — users can deepen filters / sort / grouping within a kanban view, but they cannot turn a kanban into a calendar without the entity author shipping a calendar collection first.
- The kanban-needs-a-lane-field invariant cannot be broken by user action: every saved view inherits the validated config of its base.

## Consequences

### Positive

- Adding a view to an entity is a one-line declarative change in the C# builder; the renderer comes for free.
- Misconfiguration (kanban without lane, calendar without date) fails at app boot — never at user click time.
- The closed catalog + custom namespace gives Notion-class flexibility (List/Board/Calendar/Gallery in v1) with the type-safety of an enum.
- Multiple instances per kind enable real-world UX: a kanban-by-status AND a kanban-by-assignee on the same task list, both addressable, both validated.
- The typed C# builder makes invalid wiring unrepresentable; the JSON Schema validation is a defense-in-depth backstop for `custom:` views.

### Negative / accepted trade-offs

- Tenant admins (Tier B Layer 1, ADR-040) cannot add a new view kind. They can promote saved views (`EntityView`, ADR-047) and override layout, but the catalog of available kinds is locked at compile time. Right boundary for the SaaS audit story; restrictive for "let me design a custom view from the UI" expectations (Notion-grade dynamism — out of scope per ADR-040 Tier C).
- Phase 1 ships only `list` + `kanban`; the showcase Phase 1 cobaye (Party + Invoice — see #1530, #1564, #1565) doesn't exercise calendar / gallery. Those land in Phase 1.5 with their range-query endpoint and image-field bindings.
- Phase 3 niche kinds (timeline / map / tree / gantt) ship on demand, not preemptively. The catalog reserves the names so apps can plan around them, but ADRs amending the spec land when there's a concrete consumer.

### Phasing snapshot

| Phase | Kinds shipped | Cobaye |
| ----- | ------------- | ------ |
| 1 | `list`, `kanban` | Party (list + kanban-by-status), Invoice (list + InvoiceLines tab via relations) |
| 1.5 | `calendar`, `gallery` | Activities calendar (Phase 2 prerequisite); BlobDescriptor gallery |
| 3 | `timeline`, `map`, `tree`, `gantt` (on demand) | TBD |

## Cross-references

- [ADR-040](./040-three-tier-metadata-architecture) — Three-tier metadata. The kind catalog is Tier A (compiled); user-saved views (`EntityView`) are an additive layer that cannot mutate the kind.
- [ADR-041](./041-widget-catalog) — Widget catalog. Same closed-set + `custom:<prefix>-<name>` namespace pattern, applied to field renderers.
- [ADR-047](./047-entity-view) — `EntityView` supersedes `Granit.QueryEngine.SavedViews`. Documents the immutable `basedOn` rule referenced in §5 above.

## References

- Frappe view types — research compiled in PR #1599 conversation. Convention-driven (Calendar reads `start`/`end` from a sibling JS file), no schema validation, errors surface at click time.
- Notion view types — closed (~6 in v1: Table, Board, Calendar, Gallery, Timeline, List), each with a typed config UI. Same ergonomic dividend Granit targets.
- Odoo view types — XML-based, open-ended, view inheritance via XPath. Granit deliberately rejects this approach (see [ADR-045](./045-contributor-pattern) for the typed-contribution alternative).
