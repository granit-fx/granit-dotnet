# Granit.Entities.Customization

Layer 1 tenant customization for `EntityDefinition`s, specified by
[ADR-053][adr-053]. Lets a tenant administrator reorder, regroup, and hide
compiled fields on any entity layout — never add fields (that's Layer 2,
deferred to Phase 4).

Pull this from hosts that resolve and persist customization deltas. The
manifest composer (story B4, in `Granit.Entities.Endpoints`) consumes the
reader to apply deltas at request time.

## What's inside

| Type | Role |
| ---- | ---- |
| `EntityCustomization` | Aggregate root keyed by `(TenantId, EntityName, LayoutKind)` — full-replace persistence |
| `LayoutKind` | Enum: `FormDefault`, `DetailDefault`, `List`, `Calendar`, `Gallery` |
| `LayoutDelta` | Closed base record — only `ReorderDelta`, `RegroupDelta`, `HideDelta` are accepted |
| `IEntityCustomizationReader` / `IEntityCustomizationWriter` | CQRS-split repository contracts |
| `NullEntityCustomizationReader` | Default no-op, returns `null` / empty so hosts without the EF companion still boot |
| `AddGranitEntitiesCustomization()` | DI extension — registers the `Null*` reader |

## Why "Layer 1" only

Layer 1 is the structural surface — what fields show up, in what order, in
which group. Adding fields is Layer 2 (Phase 4) and needs migration runtime,
query/export whitelisting, and per-tenant DDL — out of scope for Phase 2.

The closed delta vocabulary refuses by design the upgrade-breaking moves that
haunt Odoo Studio deployments. Validation against the live descriptor happens
at the endpoint boundary (B3); the aggregate enforces only shape-level
invariants (delta non-null, reorder anchor well-formed).

## Composition example

```csharp
// In the host's Program.cs
builder.Services
    .AddGranitEntitiesCustomization()                          // registry contract — Null reader by default
    .AddGranitEntitiesCustomizationEntityFrameworkCore();      // shipped by B2 — replaces Null reader with EF reader
```

## See also

- [ADR-053 — Layer 1 customization model][adr-053]
- `Granit.Entities.Customization.EntityFrameworkCore` (B2) — JSONB persistence
- `Granit.Entities.Customization.Endpoints` (B3) — `PUT` + audit trail
- `Granit.Entities.Endpoints` manifest composer (B4) — apply pipeline + provenance

[adr-053]: https://github.com/granit-fx/granit-dotnet/blob/develop/docs-site/src/content/docs/dotnet/architecture/adr/053-entities-customization-layer-1.md
