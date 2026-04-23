---
title: "ADR-028: Unified Data Lookup for QueryEngine Filters and Form Dropdowns"
description: "Introduce Granit.DataLookup — a single declarative primitive that feeds typeahead pickers across QueryEngine filter bars, edit-form dropdowns, and ReferenceData sets. Backed by a scoped registry, a canonical LookupItem shape, and a server-side label projection honoring 18 cultures."
sidebar:
  order: 28
  label: "028 - Unified Data Lookup"
---

> **Date:** 2026-04-23
> **Status:** Accepted
> **Authors:** Jean-Francois Meyers
> **Scope:** `Granit.DataLookup.Abstractions`, `Granit.DataLookup`,
> `Granit.DataLookup.EntityFrameworkCore`, `Granit.DataLookup.Endpoints`,
> `Granit.QueryEngine.Abstractions`, `Granit.QueryEngine.EntityFrameworkCore`,
> `Granit.ReferenceData`, `Granit.ReferenceData.EntityFrameworkCore`,
> `@granit/data-lookup`, `@granit/react-data-lookup`,
> `@granit/react-query-engine` (SmartFilter wiring), plus first adopters
> `Granit.MultiTenancy`, `Granit.Metering`, `Granit.Auditing`.

## Context

Two adjacent but historically disjoint problems plagued every admin surface
in Granit:

1. **Filter pickers** — columns in a grid that store foreign keys (`tenantId`,
   `userId`, `meterDefinitionId`) forced users to type a GUID by hand in
   `SmartFilterBar` because `FilterableField.Operators` told the frontend
   "here is an `Eq` / `In` operator", but offered no guidance on *which*
   values were valid.
2. **Edit-form dropdowns** — selects in create/edit forms (e.g. "choose a
   meter definition for this usage aggregate", "pick a tenant for this
   user") were either hard-coded constants (enums duplicated TS-side), or
   wired with per-form ad-hoc hooks and endpoints (`GET /api/v1/meters` with
   a custom projection). No convention, no cache consistency, no
   authorization gate, no localization story.

`Granit.ReferenceData` already shipped multilingual code lists (countries,
document types) with 15 `Label{Culture}` columns and its own endpoint, but
only for fixed taxonomies — not for dynamic cross-aggregate lookups, and
with no frontend primitive that could consume it uniformly with other
sources.

Both problems deserve the same shape: "fetch a paginated, searchable list
of `{ value, label }` from a source that the backend declares, enforce
auth, localize the label". Solving them in one concept is cheaper to
maintain, easier to document, and makes every module's pickers consistent
from day one.

Prior art to copy: PR #1092 added `EnumValues` to `FilterableField` for
enum-typed columns. Additive optional property, populated at metadata-emission
time, consumed by the frontend with zero mapping. The lookup descriptor
mirrors that pattern.

## Decision

Introduce a new framework module — **`Granit.DataLookup`** — that defines
a scoped registry of named sources and a canonical wire shape consumed
uniformly from QueryEngine filter metadata and edit-form components.

### 1. Canonical shapes

All sources project to a single shape — the frontend never re-maps:

```csharp
public sealed record LookupItem(
    object Value,
    string Label,                              // already localized server-side
    IReadOnlyDictionary<string, object?>? Extra = null);

public sealed record LookupResult(
    IReadOnlyList<LookupItem> Items,
    int? TotalCount = null,
    string? ContinuationToken = null);

public sealed record LookupDescriptor(
    string? Name = null,                       // registry key, preferred
    string? Endpoint = null,                   // custom URL, fallback
    LookupKind Kind = LookupKind.QueryEngine,  // QueryEngine | Simple | ReferenceData | Enum
    string? RequiredPermission = null,
    string? SearchParam = "search",
    IReadOnlyList<string>? ScopeKeys = null);
```

### 2. Registry and adapters

`ILookupRegistry` is a scoped service aggregating every `ILookupSource`
registered in DI. Three built-in adapters:

| Adapter | Package | Backing source |
| --- | --- | --- |
| `EnumLookupSource<TEnum>` | `Granit.DataLookup` | CLR enum + `Enum:{TypeName}.{Value}` localization keys (zero DB roundtrip) |
| `QueryableLookupSource<TEntity>` | `Granit.DataLookup.EntityFrameworkCore` | `IQueryable<TEntity>` with caller-supplied value/label selectors |
| `ReferenceDataLookupSource<TEntity>` | `Granit.ReferenceData` | auto-registered for every reference-data type under `ref-{kebab-case-name}` |

Custom adapters implement `ILookupSource` directly (external APIs,
computed lists, multi-source aggregators).

### 3. Endpoint contract

Mapped once at host startup via `app.MapGranitDataLookups()`:

| Method | Route | Purpose |
| --- | --- | --- |
| `GET` | `/api/granit/lookups` | Discovery manifest of every registered source |
| `GET` | `/api/granit/lookups/{name}` | Paginated typeahead search (`search`, `page`, `pageSize`, `scope.*`) |
| `GET` | `/api/granit/lookups/{name}/resolve?value=…` | Single-item resolve for rehydrating a persisted FK value |

Every response honors the `Accept-Language` header (labels projected
server-side), the ambient `ICurrentTenant`, and the per-source
`RequiredPermission` (auto-injected `401`/`403` from
`ProblemDetailsResponseOperationTransformer`).

### 4. QueryEngine integration

`FilterableField` gains `LookupDescriptor? Lookup` — additive, null by
default, emitted in `/meta` when the column declares
`.Lookup("name", …)` on its `ColumnBuilder`. No impact on existing
fields. Frontend receives it as `FilterableField.lookup` in the TS type.

### 5. Scoped lookups — Empty Scope Trap mitigation

A scoped lookup (e.g. `meter-definitions` filtered by `tenantId`) declares
its required scope keys. **Both layers enforce the guard**:

- **Backend**: the endpoint validates that every declared scope key is
  present and non-empty in the query string. Missing keys return
  `400 Bad Request` with a `problem+json` body — no silent full-table scan.
- **Frontend**: `useLookup` flips `enabled: false` as long as any declared
  scope key is unresolved. No HTTP call fires, and the picker component
  surfaces `missingScopeKey` so the UI can render an informative
  placeholder (*"Select a tenant first"*) rather than an open picker with
  a pending spinner.

This pattern is what makes cascading pickers safe — selecting a tenant
automatically unlocks its meter-definitions dropdown, and the user never
sees cross-tenant data.

### 6. Frontend SDK

Two new workspace packages:

- **`@granit/data-lookup`** — framework-agnostic types and axios helpers.
  Shared `findMissingScopeKey` / `isScopeSatisfied` guards usable outside
  React.
- **`@granit/react-data-lookup`** — React Query hooks (`useLookup`,
  `useLookupResolve`) and three headless render-prop components
  (`<LookupSelect>`, `<LookupPicker>`, `<LookupBadge>`). Culture-segmented
  cache keys so switching language invalidates the previously fetched
  labels.

`@granit/react-query-engine`'s `useSmartFilter` exposes
`selectedFieldLookup` so consumers branch to `<LookupPicker>` during the
`enterValue` phase whenever the selected field declares a lookup —
otherwise the existing enum / boolean / free-text flow runs.

### 7. Label localization

A single path: the server projects the label in the caller's
`CultureInfo.CurrentUICulture` before sending. `EnumLookupSource` uses
`IStringLocalizer` keyed by `Enum:{TypeName}.{Value}`;
`ReferenceDataLookupSource` reuses the existing virtual
`ReferenceDataEntity.Label` property which resolves
`Label{Culture}` → `LabelEn` → `Code`; `QueryableLookupSource` honors
whatever the caller's label selector resolves to. Eighteen cultures
supported end-to-end (15 base + 3 regional variants).

## Evaluated alternatives

- **Frontend-side registry keyed by source URL.** Each form imports its
  own hook pointing at a per-module endpoint. Rejected: drifts from the
  backend contract, fragments permission/localization logic across the UI.
- **Embed lookup items inline in `/meta`.** Doesn't scale (lookup tables
  can hold thousands of tenants), breaks when permissions filter the set
  at user boundary, and bloats every metadata payload.
- **GraphQL schema with reference types.** Over-engineers the REST+JSON
  contract Granit already ships; requires a second runtime and tooling.
- **Scoped lookups resolved by DSL in the descriptor** (e.g.
  `scope: { tenantId: "@parent.tenantId" }`). Rejected: pushes string
  parsing into the SDK, complicates testing, and couples the hook to a
  specific form library. The `scopeKeys` + `<LookupSelect cascadeFrom>`
  pattern keeps the SDK dumb and lets React Hook Form (or any alternative)
  own the form-field subscription.

## Justification

- **Declarative and co-located.** The same `ColumnBuilder.Lookup(...)`
  call both declares the picker shape and documents the intent next to the
  column it governs. `FilterableField.lookup` then flows end-to-end
  without custom wiring.
- **Reuses existing machinery.** `QueryableLookupSource<T>` is a thin
  projection over any `IQueryable<T>`; `ReferenceDataLookupSource<T>`
  reuses the already-paginated reference-data reader; `EnumLookupSource`
  is zero-IO. No new database concept.
- **Additive and null-tolerant.** `FilterableField.Lookup` is optional;
  frontends that ignore it keep working. A missing registration simply
  yields a `404` on the endpoint with no server-side side effect.
- **Permission-safe.** Per-source `RequiredPermission` is enforced by the
  dispatch handler before the source is invoked; the frontend uses the
  manifest to hide pickers from users without the permission.
- **Multilingual by default.** The label is already in the caller's
  culture by the time the frontend sees it. No fallback logic, no key
  leakage.

## Consequences

**Positive:**

- Uniform UX across every admin page — filter pickers, edit-form selects,
  and read-only value badges all share the same lookup infrastructure.
- Zero per-form boilerplate for new adopters (one registration + one
  fluent `.Lookup("name")` in the existing `QueryDefinition`).
- Cross-language cache segmentation out of the box — switching locale
  invalidates stale labels automatically.

**Negative / constraints:**

- **Authorization coupling:** the lookup endpoint must be reachable by any
  principal who can filter by the column. Mitigated by the explicit
  `RequiredPermission` declaration; enforced twice (manifest + dispatch).
- **Latency per field.** Each picker round-trips on every keystroke
  (debounced). React Query cache and the 30s default `staleTime` hide
  most of that, but offline/ultra-low-bandwidth use cases remain limited.
- **Naming discipline.** Source names are a magic string matched at
  runtime; a rename breaks every consumer. Tests enforce uniqueness at
  registration time, and the manifest endpoint makes orphaned
  descriptors easy to detect in CI.

## Follow-up work

Delivered in seven sequential PRs (all merged as of 2026-04-23):

1. Foundation — 4 packages + 4 test projects (36 tests).
2. QueryEngine integration — `FilterableField.Lookup`,
   `ColumnBuilder.Lookup()`, metadata propagation (12 new tests).
3. ReferenceData bridge — auto-register every refdata type as a lookup
   source under `ref-{kebab}` (18 new tests).
4. Frontend SDK — `@granit/data-lookup` + `@granit/react-data-lookup`
   (47 tests after coverage follow-up).
5. SmartFilter wiring — `selectedFieldLookup` on `useSmartFilter`
   (5 new tests).
6. First backend adopters — Tenant, MeterDefinition (source), TenantId +
   MeterDefinitionId (consumer), AuditEntry.TenantId (consumer).
7. Documentation — this ADR + `docs/guide/patterns/data-lookup.md`.

Follow-up items explicitly out of scope:

- User lookup (requires Identity.Federated and Identity.Local to agree on
  a shared projection).
- Showcase migrations (`meter-form.tsx`, `create-invoice-dialog.tsx`) —
  depend on enum lookup sources being registered in the showcase host.
- Cursor-based lookup sources for datasets over 50k rows.

## Related

- [ADR-020: Declarative definitions placement](/dotnet/architecture/adr/020-declarative-definitions-placement/)
  — why `QueryDefinition` lives in base modules (applies to
  `AsLookup`-decorated definitions too).
- [`/dotnet/business/data-lookup/`](/dotnet/business/data-lookup/) —
  module documentation page.
- [`/dotnet/guide/patterns/data-lookup/`](/dotnet/guide/patterns/data-lookup/)
  — adoption guide with recipes.
