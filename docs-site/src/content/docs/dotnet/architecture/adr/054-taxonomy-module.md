---
title: "ADR-054: Granit.Taxonomy — cross-cutting tags and categories"
description: "Granit introduces a new Granit.Taxonomy module providing tags and hierarchical categories as a cross-cutting taxonomy primitive for any aggregate root. Tags are scoped per domain (documents, parties, activities…) so each module gets a focused autocomplete, but live in a single shared table to enable cross-entity search and a unified admin UI. Hosts that prefer a single global pot collapse to scope=\"global\"."
sidebar:
  order: 54
  label: "054 - Taxonomy module"
---

> **Date:** 2026-05-02
> **Authors:** Jean-Francois Meyers
> **Scope:** NEW modules `Granit.Taxonomy`, `Granit.Taxonomy.EntityFrameworkCore`, `Granit.Taxonomy.Endpoints`. Replaces the per-module `*Tag` join tables originally specced by ADR-052 (Documents) and the inline tag fields used by other modules.
> **Status:** Accepted

## Context

Tagging and categorisation are universal. The first concrete need surfaced through `Granit.Documents` (ADR-052), where the F5 stories specced a `DocumentTag` join with a per-module `Tag` aggregate. But documents are not the only entity that needs tags:

- `Granit.Parties` — customer/contact tags (VIP, prospect, supplier-tier-1)
- `Granit.Activities` — task/calendar labels (urgent, blocked, needs-review)
- `Granit.Invoicing` — analytical categories on invoices and lines
- A future Knowledge Base module — article topics
- A future Products module — product family taxonomy
- Hosting apps — domain-specific taxonomies the framework cannot enumerate up front

If each module ships its own `Tag` aggregate plus a `*Tag` join table, the framework duplicates the same infrastructure six or more times: CRUD endpoints, autocomplete, admin UI, archi tests, localisation, colour pickers. Worse, there is no cross-entity search — a host wanting to answer "show me everything tagged urgent across documents, activities, and invoices" has to query each module separately and merge client-side.

Categories follow the same logic. They differ from tags in shape (single-assignment, hierarchical) but share the cross-cutting nature: documents have categories, products have categories, knowledge-base articles have categories. Building one `Category` aggregate per consumer module is the same waste.

A separate market scan confirms the pattern is solved by extracting a shared taxonomy primitive:

| System | Approach |
| ------ | -------- |
| **Odoo** | Per-domain models (`crm.tag`, `res.partner.category`, `project.tags`, `account.analytic.tag`, `hr.employee.category`). Pots are separated for UX clarity but share no infrastructure. |
| **GitHub** | Labels per-repository — scoped, no cross-repo aggregation. |
| **Linear** | Labels per-team with optional "shared across teams" flag. |
| **Notion** | Tags per-database (per-collection scope). |
| **Asana** | Tags per-workspace. |
| **Jira** | Free-form string labels with no model — too minimalist; no colour, no description, no canonical list. |

Almost every successful product **scopes** tags rather than dumping them in one global pot. A single mega-pot turns autocomplete into noise: a "VIP" tag for a customer means something different from a "VIP" tag for a project, and forcing them into the same dropdown loses semantic context. But Odoo's per-domain-model approach trades that UX clarity for missing cross-cutting search and duplicated infrastructure.

This ADR locks a hybrid: **one shared module with one physical table, but each tag carries a scope discriminator.** Each consumer module's autocomplete filters by its own scope; the shared admin UI groups by scope; cross-entity search opts into ignoring scope. Hosts that genuinely want a single global pot collapse all tags to `scope = "global"`.

## Decision drivers

- **Cross-cutting concern.** Every aggregate root in a multi-module framework is a potential tag target. Building this once saves N implementations.
- **UX-first.** Autocomplete must stay scoped per domain by default — Odoo's lesson, validated across GitHub, Linear, Notion.
- **Cross-entity search must be possible.** Power users want "everything tagged urgent" without N round trips.
- **Solo-maintainer cost.** One aggregate, one DbContext, one CRUD, one set of localisations — versus six per-module duplications.
- **Pre-1.0 freedom.** ADR-052's `DocumentTag` design has not been built yet; reshaping F5 stories costs nothing.
- **Polymorphic FK trade-off.** Polymorphism on the assignment table sacrifices DB-level referential integrity on the target id, but the discriminator + composite index `(TenantId, TargetType, TargetId)` makes lookups efficient and orphan cleanup happens via integration events.

## Considered alternatives

### A — Tag aggregate per module (status quo from ADR-052)

Each module ships its own `Tag` + `*Tag` join (`DocumentTag`, `PartyTag`, `ActivityTag`…).

- **Pros**: simple, FK integrity intact, no polymorphism.
- **Cons**: massive duplication (CRUD, admin UI, autocomplete, archi tests, localisation × N modules), no cross-entity search, no shared "Tag" admin surface.
- **Rejected.**

### B — `Granit.Taxonomy` cross-cutting (chosen)

One `Tag` aggregate + one `TagAssignment(TargetType, TargetId)` polymorphic table. Modules wire by mapping their entity type to a `TargetType` discriminator string. Categories share the same module with a hierarchical `Category` aggregate.

- **Pros**: one infrastructure, one admin UI, native cross-entity search, shared Granit.Taxonomy.Notifications, one set of permissions.
- **Cons**: no DB-level FK on `TargetId`, host must add an integration event handler to clean up orphan assignments when the target entity is permanently deleted. Polymorphism requires a discriminator the framework cannot enumerate exhaustively.
- **Chosen** with mitigations: the cleanup is solved by a generic `IEmitEntityLifecycleEvents` listener (Granit already emits `EntityDeletedEto<T>`), and the discriminator is the assembly-qualified type name of the target aggregate.

### C — `Granit.Taxonomy.Abstractions` shared, per-module joins kept

Shared `Tag` / `Category` base aggregates in `Granit.Taxonomy.Abstractions`, but each module still owns a typed `*Tag` / `*Category` join table.

- **Pros**: FK integrity preserved, shared Tag/Category definitions and admin UI.
- **Cons**: still N join tables to maintain. Cross-entity search requires a `UNION ALL` across N tables — possible, but ugly. Querying "all tags assigned anywhere" needs the same union.
- **Rejected**: the FK integrity gain does not justify the operational cost of N tables.

## Decision

Introduce three NEW packages:

| Package | Role |
| ------- | ---- |
| `Granit.Taxonomy` | Abstractions: `Tag` aggregate, `Category` aggregate, `TagAssignment` entity, `CategoryAssignment` entity, `ITagService`, `ICategoryService`, options, DI registration, diagnostics. |
| `Granit.Taxonomy.EntityFrameworkCore` | Isolated `TaxonomyDbContext`, EF Core configurations, EF Core implementations of `ITagService` / `ICategoryService`, archi-test-required interceptors. |
| `Granit.Taxonomy.Endpoints` | Tag & category CRUD, assignment endpoints (`POST /tags/{tagId}/assign`, `DELETE /tags/{tagId}/assign/{targetType}/{targetId}`), per-scope autocomplete, cross-entity search. |

### Tag aggregate shape

```csharp
public sealed class Tag : AggregateRoot, IMultiTenant
{
    public Guid? TenantId { get; private set; }

    /// <summary>
    /// Scope discriminator: "documents", "parties", "activities", "global", or any
    /// host-defined string. Drives the per-domain autocomplete filter and the admin
    /// UI grouping. Hosts that want a single global pot use scope = "global"
    /// everywhere; modules that want true isolation use their own scope.
    /// </summary>
    public string Scope { get; private set; }

    public string Name { get; private set; }      // max 50 chars
    public string Color { get; private set; }     // hex (#RRGGBB)

    /// <summary>
    /// Operational tag — visible in admin UI and assignment forms (so it can be
    /// applied), but hidden on the entity card / list / detail surfaces.
    /// Equivalent to Odoo's `hide_in_kanban` but generalised to any "card" surface.
    /// Default: false.
    /// </summary>
    public bool HideOnEntityCard { get; private set; }

    // Unique constraint: (TenantId, Scope, Name). "VIP" can coexist in scope
    // "parties" and scope "global" without conflict.
}
```

Notably **dropped** versus a typical "fully-loaded" tag aggregate:

- `Description` — Odoo doesn't carry one on `crm.tag` either; the name is the entire UX. Dropping description avoids unnecessary localisation overhead and a clutter form field.
- `Active` flag — soft-delete via Granit's standard `Trashed` semantic if needed in a later phase; the F5 baseline keeps tags either present or hard-deleted.
- `ParentId` — tags are flat by design. Hierarchy belongs to `Category`.

### TagAssignment shape

```csharp
public sealed class TagAssignment : Entity, IMultiTenant
{
    public Guid? TenantId { get; private set; }
    public Guid TagId { get; private set; }

    /// <summary>
    /// Discriminator for the polymorphic target. By convention the assembly-qualified
    /// type name of the target aggregate (e.g. "Granit.Documents.Domain.Document").
    /// Indexed alongside TargetId for `WHERE TargetType = ? AND TargetId = ?` lookups.
    /// </summary>
    public string TargetType { get; private set; }
    public Guid TargetId { get; private set; }

    public DateTimeOffset AssignedAt { get; private set; }
    public Guid AssignedByUserId { get; private set; }

    // Unique constraint: (TenantId, TagId, TargetType, TargetId).
    // Composite index: (TenantId, TargetType, TargetId) for "tags of entity X".
    // Composite index: (TenantId, TagId) for "things tagged X".
}
```

### Category aggregate shape

Categories are hierarchical and single-assignment. Same shape as `Tag` plus a
`ParentId?` and a materialised `Path` for fast subtree queries (mirrors the
`Folder` pattern from ADR-052):

```csharp
public sealed class Category : AggregateRoot, IMultiTenant
{
    public Guid? TenantId { get; private set; }
    public string Scope { get; private set; }
    public Guid? ParentId { get; private set; }    // null = root category in the scope
    public string Path { get; private set; }       // materialised: "/electronics/laptops"
    public int Depth { get; private set; }
    public string Name { get; private set; }
    public string? IconName { get; private set; }  // optional icon (Lucide identifier)
    public bool HideOnEntityCard { get; private set; }
}
```

`CategoryAssignment` follows the same polymorphic pattern as `TagAssignment` but
with a unique constraint on `(TenantId, TargetType, TargetId)` (one category per
target, not many).

### Polymorphic FK cleanup

When a target aggregate is permanently deleted, its tag and category assignments
become orphans. Cleanup is handled by a generic listener subscribed to
`EntityDeletedEto<T>` (already emitted by Granit aggregates that opt in via
`IEmitEntityLifecycleEvents`):

```csharp
public class TaxonomyAssignmentCleanupHandler
{
    public static async Task HandleAsync(
        EntityDeletedEto evt,
        ITagAssignmentService tags,
        ICategoryAssignmentService categories,
        CancellationToken cancellationToken)
    {
        await tags.RemoveAllAssignmentsAsync(evt.EntityType, evt.EntityId, cancellationToken).ConfigureAwait(false);
        await categories.RemoveAllAssignmentsAsync(evt.EntityType, evt.EntityId, cancellationToken).ConfigureAwait(false);
    }
}
```

A nightly background job (planned, Feature T5) sweeps for orphan rows that
escaped the event path (e.g. raw SQL deletes), guaranteeing eventual consistency
without DB-level FKs.

### Endpoints shape

| Verb | Route | Purpose |
| ---- | ----- | ------- |
| `GET` | `/api/v1/taxonomy/tags?scope=documents&q=urg` | Per-scope autocomplete (default scope mandatory; cross-scope opt-in via `?scope=*`) |
| `POST` | `/api/v1/taxonomy/tags` | Create tag (admin) |
| `PATCH` | `/api/v1/taxonomy/tags/{id}` | Rename / recolour / toggle `HideOnEntityCard` |
| `DELETE` | `/api/v1/taxonomy/tags/{id}` | Delete tag + cascading assignment removal |
| `POST` | `/api/v1/taxonomy/tags/{id}/assign` | Body: `{ targetType, targetId }`. Idempotent. |
| `DELETE` | `/api/v1/taxonomy/tags/{id}/assign/{targetType}/{targetId}` | Remove assignment |
| `GET` | `/api/v1/taxonomy/assignments?targetType=…&targetId=…` | Tags currently assigned to a target |
| `GET` | `/api/v1/taxonomy/search?q=urgent&scope=*` | Cross-entity search: returns assignments grouped by `TargetType` |

Categories mirror the same shape under `/api/v1/taxonomy/categories`.

### Permissions

Three permission groups:

- `Taxonomy.Tags.Read` / `Taxonomy.Tags.Manage` (CRUD + assign)
- `Taxonomy.Categories.Read` / `Taxonomy.Categories.Manage`
- `Taxonomy.Search.Read` (cross-entity search — separate because it surfaces data the user might not have per-entity read on)

Per-entity tagging additionally requires the consumer module's own permission
(e.g. assigning a tag to a `Document` requires `Documents.Documents.Manage`). The
endpoint enforces this composite check by resolving the `targetType` to the
expected permission via a registered `ITaggablePermissionResolver`.

### Consumer module integration

A consumer module (e.g. `Granit.Documents`) integrates with Taxonomy by:

1. Adding `<ProjectReference Include="..\Granit.Taxonomy\Granit.Taxonomy.csproj" />` in its base csproj.
2. Registering a `TaggableTypeRegistration` mapping its aggregate type to a stable scope and discriminator: `services.AddTaggableEntity<Document>(scope: "documents")`.
3. Optionally exposing convenience endpoints on its own surface (`GET /documents/{id}/tags` proxies to `/taxonomy/assignments?...`) — purely a UX shortcut, the canonical store remains Taxonomy.

The consumer never defines its own `*Tag` table.

### Localisation

Tag and category **labels** are tenant data — never localised by the framework
(a tenant-defined "VIP" stays "VIP" in every language). Only the **permission
strings** and **error messages** of `Granit.Taxonomy.Endpoints` get the standard
18-culture treatment. This matches Odoo's behaviour and avoids a translation
matrix that would be unmanageable for tenant-facing administrators.

## Phasing

| Feature | Stories | Outcome |
| ------- | ------- | ------- |
| **T1 — Module scaffolding** | T1.1 (csproj + module class), T1.2 (EF Core + Tag aggregate + tenant-scoped uniqueness), T1.3 (diagnostics: `TaxonomyMetrics` + `TaxonomyActivitySource`) | The three packages exist, build, ship `Tag` and `TagAssignment` schemas, but no endpoints yet. |
| **T2 — Tag endpoints** | T2.1 (CRUD + per-scope autocomplete), T2.2 (assignment endpoints + idempotency) | A consumer module can wire up tag CRUD via the Taxonomy API. |
| **T3 — Cross-entity search** | T3.1 (`/api/v1/taxonomy/search` endpoint with TargetType-grouped results) | Power-user search across all taggable entities. |
| **T4 — Category aggregate** | T4.1 (`Category` aggregate + tree path materialisation), T4.2 (Category CRUD + assignment + autocomplete) | Hierarchical taxonomy delivered. Optional from a host perspective. |
| **T5 — Orphan-assignment cleanup job** | T5.1 (`TaxonomyAssignmentCleanupHandler` for `EntityDeletedEto`), T5.2 (nightly sweep job) | Eventual consistency on deletions that bypass the event path. |
| **T6 — Documents wire-up** | T6.1 (replaces ADR-052 F5.1/F5.2/F5.3): register `Document` as a taggable entity, expose proxy endpoints, drop the never-built `DocumentTag` plan. | Documents users can tag/untag and search by tag through the Taxonomy module. |

ADR-052 is updated to reference this ADR for the tagging story; the F5
sub-stories (originally `Tag entity`, `DocumentTag join`, `Search endpoint`) are
re-scoped under T6.

## Consequences

### Positive

- Single source of truth for tags and categories across the whole framework.
- Cross-entity search becomes a single SQL query.
- Per-scope autocomplete keeps the per-domain UX clarity Odoo's users value.
- Hosts that prefer a single global pot collapse to `scope = "global"` everywhere — zero code change in Taxonomy.
- One admin UI, one localisation set, one set of archi tests.
- The pattern is reusable for any future "cross-cutting label" concern (favourites, watchers, custom fields).

### Negative

- No DB-level FK on `TargetId`. Mitigated by the cleanup handler + nightly sweep.
- The `TargetType` discriminator string couples to the producer's type name; renaming a target aggregate requires a one-shot data migration.
- Cross-tenant tag pollution is impossible (tenant filter applies), but cross-scope pollution **is** possible if a host registers two modules under the same scope — caught by archi test (`TaggableTypeRegistration` uniqueness).

### Migration

None — `Granit.Documents` F5 stories were never built; they are simply re-scoped under T6.
Existing modules with no tag plan are unaffected.
