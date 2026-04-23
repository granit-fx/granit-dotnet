---
title: "Data Lookup Pattern — Unified Typeahead Pickers"
description: "One declarative primitive (Granit.DataLookup) feeds QueryEngine filter pickers, edit-form dropdowns, and ReferenceData sets from a single registry. Server-side label localization, scoped dependencies, and zero frontend boilerplate."
sidebar:
  label: Data Lookup
  order: 58
---

## Definition

The **Data Lookup pattern** centralizes the "pick a value from a remote
set" interaction across every admin surface. A backend registry maps
named sources (`tenants`, `meter-definitions`, `ref-countries`,
`enum-aggregation-type`) to concrete implementations; a canonical wire
shape carries `{ value, label, extra? }` to the frontend; a headless SDK
renders combobox-style pickers wired to React Query with automatic
scope-dependency handling.

Same primitive, three consumption sites:

1. **QueryEngine filter bar** — a filterable column declares
   `.Lookup("name")` and `SmartFilter` routes the "enter value" phase to
   the registry-backed picker.
2. **Edit-form dropdowns** — forms use `<LookupSelect lookup="name" />`
   with scope bound to sibling form fields via `cascadeFrom`.
3. **Read-only projections** — detail views use `<LookupBadge />` to
   rehydrate a stored foreign-key value into a localized label.

## Diagram

```mermaid
flowchart LR
    subgraph Backend["Granit.DataLookup"]
        Reg[ILookupRegistry<br/>scoped]
        SE[QueryableLookupSource&lt;T&gt;]
        RE[ReferenceDataLookupSource&lt;T&gt;]
        EE[EnumLookupSource&lt;TEnum&gt;]
        Reg --> SE
        Reg --> RE
        Reg --> EE
    end

    EP[GET /api/granit/lookups/&#123;name&#125;]
    MF[FilterableField.Lookup<br/>in /meta]

    Reg --> EP
    EE -.label: Enum:TypeName.Value.-> Loc[IStringLocalizer]
    RE -.Label&#123;Culture&#125; column.-> Loc

    subgraph Frontend["@granit/react-data-lookup"]
        UL[useLookup]
        LS[&lt;LookupSelect&gt;]
        LP[&lt;LookupPicker&gt;]
        LB[&lt;LookupBadge&gt;]
        LS --> UL
        LP --> UL
        LB --> ULR[useLookupResolve]
    end

    EP --> UL
    EP --> ULR
    MF --> LP
```

## Canonical shapes

The backend returns exactly one response shape regardless of source kind:

```csharp
public sealed record LookupItem(
    object Value,
    string Label,                              // already localized server-side
    IReadOnlyDictionary<string, object?>? Extra = null);

public sealed record LookupResult(
    IReadOnlyList<LookupItem> Items,
    int? TotalCount = null,
    string? ContinuationToken = null);
```

The descriptor that modules emit alongside column / field metadata:

```csharp
public sealed record LookupDescriptor(
    string? Name = null,                       // registry key, preferred
    string? Endpoint = null,                   // custom URL, fallback
    LookupKind Kind = LookupKind.QueryEngine,  // QueryEngine | Simple | ReferenceData | Enum
    string? RequiredPermission = null,
    string? SearchParam = "search",
    IReadOnlyList<string>? ScopeKeys = null);
```

## Backend adoption — three recipes

### Recipe 1 — expose a DbSet as a lookup source

Host-level registration inside the module's `.EntityFrameworkCore`
builder extension:

```csharp
builder.Services.AddQueryableLookup<Tenant, MultiTenancyDbContext>(
    name: "tenants",
    valueSelector: t => t.Id,
    labelSelector: t => t.Name,
    searchPredicate: (t, search) => t.Name.Contains(search) || t.Identifier.Contains(search),
    requiredPermission: "Platform.Tenants.Read");
```

### Recipe 2 — scoped lookup (cascading pickers)

A source whose result set depends on a parent field declares its required
scope keys; the runtime enforces they are provided on every request:

```csharp
builder.Services.AddQueryableLookup<MeterDefinition, MeteringDbContext>(
    name: "meter-definitions",
    valueSelector: m => m.Id,
    labelSelector: m => m.Name,
    searchPredicate: (m, search) => m.Name.Contains(search) || m.Unit.Contains(search),
    scopeKeys: ["tenantId"]);
```

A query column then chains the dependency:

```csharp
.Column(u => u.MeterDefinitionId, c => c
    .Label("Meter")
    .Filterable()
    .Lookup("meter-definitions", scopeKeys: ["tenantId"]))
```

### Recipe 3 — CLR enum as a lookup

Enums stay source-of-truth in code; the runtime resolves their labels via
localization keys (`Enum:{TypeName}.{Value}` across 18 cultures):

```csharp
services.AddEnumLookup<AggregationType>(
    name: "enum-aggregation-type",
    requiredPermission: "Metering.Meters.Read");
```

## Consuming a lookup from a QueryEngine column

A single fluent call on any `ColumnBuilder<T>`:

```csharp
.Column(m => m.TenantId, c => c
    .Label("Tenant")
    .LabelKey("Metering.Columns.Tenant")
    .Filterable()
    .Sortable()
    .Lookup("tenants", requiredPermission: "Platform.Tenants.Read"))
```

The descriptor rides `FilterableField.Lookup` into the `GET /meta`
payload; `SmartFilter` (headless React hook) surfaces it as
`selectedFieldLookup`, and the calling component routes the "enter
value" phase to `<LookupPicker>`.

## Frontend — headless render-prop components

```tsx
import { LookupSelect } from '@granit/react-data-lookup';

<LookupSelect
  descriptor={{ name: 'tenants' }}
  value={tenantId}
  onChange={setTenantId}
  client={axios}
  culture={i18n.resolvedLanguage}
  render={({ items, search, setSearch, selectedItem, onChange, missingScopeKey }) =>
    missingScopeKey ? (
      <span>Select {missingScopeKey} first.</span>
    ) : (
      <Combobox value={selectedItem?.value} onChange={onChange}>
        <Combobox.Input value={search} onChange={(e) => setSearch(e.target.value)} />
        <Combobox.Options>
          {items.map((item) => (
            <Combobox.Option key={String(item.value)} value={item.value}>
              {item.label}
            </Combobox.Option>
          ))}
        </Combobox.Options>
      </Combobox>
    )
  }
/>
```

### Cascading picker with `react-hook-form`

```tsx
const { watch } = useFormContext();
const tenantId = watch('tenantId');

<LookupSelect
  descriptor={{ name: 'meter-definitions', scopeKeys: ['tenantId'] }}
  value={meterId}
  onChange={setMeterId}
  scope={{ tenantId }}  // re-renders when tenantId changes
  client={axios}
  render={/* consumer UI */}
/>
```

When `tenantId` is empty, the SDK disables the request entirely —
`useLookup` flips `enabled: false`, no HTTP call fires, and the render
prop receives `missingScopeKey = "tenantId"` so the UI can render an
informative placeholder.

## Empty Scope Trap — defensive design

Scoped sources are dangerous if the frontend forgets to provide the
scope value: the backend would otherwise either return an unrelated
full list (multi-tenant leak!) or 500. The pattern enforces the guard
twice:

- **Backend** — the endpoint validates every declared scope key before
  invoking the source; missing keys return `400 Bad Request` with a
  `problem+json` listing the missing names.
- **Frontend** — `useLookup` checks `ScopeKeys` against the incoming
  `scope` object and keeps React Query `enabled: false` until every key
  has a non-empty value. No HTTP call is ever emitted against an
  incomplete scope.

## Localization — server-side projection

The label is always resolved by the server in the caller's
`CultureInfo.CurrentUICulture` (honoring the `Accept-Language` header)
before being serialized. The frontend never re-translates.

| Source | Label resolution |
| --- | --- |
| `EnumLookupSource<TEnum>` | `IStringLocalizer[$"Enum:{TypeName}.{Value}"]`, falls back to enum name |
| `ReferenceDataLookupSource<T>` | Virtual `ReferenceDataEntity.Label` — switches on `TwoLetterISOLanguageName`, falls back to `LabelEn`, then `Code` |
| `QueryableLookupSource<T>` | Whatever the caller's `labelSelector` returns |

React Query's `queryKey` in `@granit/react-data-lookup` includes the
culture so caches are per-language; a locale switch invalidates stale
labels automatically.

## When to use vs when to avoid

**Use Data Lookup when:**

- A filterable column stores a foreign key (`Guid tenantId`, `string countryCode`).
- An edit form needs a dropdown whose options aren't hardcoded enums in
  TypeScript.
- A detail view shows a FK value and needs a localized label.
- A reference-data taxonomy (countries, document types) has to be
  exposed uniformly with other pickers.

**Avoid when:**

- The candidate set has fewer than ~20 items **and** is never tenant- or
  permission-scoped — inline `<SelectItem>` tags beat a round-trip.
- The source comes from an external API with no authorization model and
  no multi-tenant isolation — wire the external client directly.
- The "value" is a complex object that cannot be serialized as JSON
  (`Value` is typed as `object`, but stringified round-trip is assumed).

## Related

- [ADR-028: Unified Data Lookup](/dotnet/architecture/adr/028-unified-data-lookup/)
  — architectural rationale and alternatives evaluated.
- [`/dotnet/business/data-lookup/`](/dotnet/business/data-lookup/) —
  module reference with full API surface.
- [`/dotnet/business/query-engine/`](/dotnet/business/query-engine/) —
  how `FilterableField.Lookup` fits into the metadata pipeline.
- [`/dotnet/business/reference-data/`](/dotnet/business/reference-data/) —
  auto-registered `ref-*` sources for code lists.
