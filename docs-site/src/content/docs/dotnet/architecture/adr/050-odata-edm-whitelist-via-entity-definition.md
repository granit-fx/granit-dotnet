---
title: "ADR-050 — OData EDM whitelist via EntityDefinition"
description: "The Granit.Http.ODataExposure module derives its EDM property whitelist from the registered EntityDefinition (gate) and the referenced ExportDefinition (field source). Convention-based AddEntityType is rejected because it leaks framework-internal collections (DomainEvents, IntegrationEvents) into $metadata."
sidebar:
  label: ADR-050 OData EDM via EntityDefinition
  order: 50
---

**Status**: Accepted — 2026-05-01
**Deciders**: JF Meyers
**Issue**: [#1668](https://github.com/granit-fx/granit-dotnet/issues/1668) (data-extraction contract)
**Parent**: [#1369](https://github.com/granit-fx/granit-dotnet/issues/1369) (FEATURE — OData v4 connector)

## Context

`Granit.Http.ODataExposure` v1 builds the EDM model via
`ODataConventionModelBuilder.AddEntityType(typeof(TEntity))`. The convention
builder reflects on every public CLR property of the entity, which means
`AggregateRoot` implementors (every business entity in the framework) leak
their `IDomainEventSource` / `IIntegrationEventSource` contracts into
`$metadata`:

```xml
<Property Name="DomainEvents" Type="Collection(Granit.Events.IDomainEvent)"/>
<Property Name="IntegrationEvents" Type="Collection(Granit.Events.IIntegrationEvent)"/>
```

These properties are visible to every BI consumer (Power BI's Navigator
shows them as queryable fields), they fail to translate to anything useful
when included in `$select`, and they expose framework-internal contracts
the application never intended to publish.

The leak is one symptom of a broader gap: there is no explicit
**data-extraction contract** between the application's entity model and
the OData surface. The convention-based approach treats every public
property as fair game, which is the wrong default for a security-sensitive
boundary.

## Decision drivers

- **Defense-in-depth at field level (ADR-040).** Some fields are
  legitimate columns on the admin grid but must NOT cross the OData
  boundary unless the caller carries a specific permission
  (e.g. `Customer.RiskScore`, `User.PasswordHash`).
- **Cross-module navigation (ADR-048).** OData consumers want
  `NavigationProperty` declarations matching the entity graph
  (`Invoice → Customer`) so Power BI's relationship view works.
  Hand-rolling these from each module's EDM is wasteful when
  `EntityDefinition.Relations` already declares them.
- **Mental model.** OData = "API de données pour le métier" — an
  ongoing, queryable, navigable surface. Distinct from `Export` whose
  job is "ship a CSV/XLSX file for one specific use case". The two
  surfaces share fields today but will diverge.
- **Solo-maintainer cost of doing this twice.** Pinning OData on
  `Export` today means re-pinning to `EntityDefinition` once
  navigation lands. Two refactors, two ADRs, two test rewrites.

## Considered alternatives

### A — Use `QueryDefinition.GetColumns()`

`QueryDefinition` declares the admin-grid filterable / sortable columns.
Typed property selectors (Expression-based), so type info is recoverable.

**Rejected.** A strict subset of "what an entity exposes". Modules
routinely omit non-filterable but BI-relevant fields (`Subtotal`,
`TaxTotal`, `AmountPaid` per #1584). Forces module owners to make a
column `Filterable()` purely to get it onto OData — pollutes the
QueryDefinition.

### B — Use `ExportDefinition.GetFields()` directly

`ExportDefinition` carries the "what we accept to extract from the
system" semantics — a clean match for BI. The pairing rule (ADR-020)
guarantees every admin-visible entity has one.

**Rejected as the primary contract** but kept as the field source
under (C). Reasons it is not the right primary contract:

- **No field-level `RequiresPermission`.** Adding it to
  `ExportFieldBuilder` works mechanically but duplicates the same
  declaration that already lives on `EntityDefinition.Forms` /
  `Details` `FieldBuilder`. Two sources of truth for the same
  field-permission decision is a drift accelerator.
- **No cross-module navigation.** `ExportFieldDescriptor.PropertyPath`
  flattens `"Customer.Name"`. Generating OData `NavigationProperty`
  from these flat paths would require a parallel relation declaration
  somewhere — `EntityDefinition.Relations` is that somewhere.
- **Diverging audiences.** Export will gain CSV-specific semantics
  (encoding, separators, cell formatting); OData will gain
  navigation, filter pushdown, expansion. Pinning OData to Export
  freezes the contract on Export's mental model.

### C — Use `EntityDefinition` as gate, `ExportDefinition` as field source (CHOSEN)

`EntityDefinition` orchestrates the entity surface — references
`Query`, `Export`, `Metric`, `Dashboard`, `Workflow`, declares `Forms`,
`Details`, `Relations`. The OData EDM consumer:

1. Resolves the `EntityDefinition` for each registered EntitySet's
   entity type. Throws at startup when missing — strict-config gate.
2. Follows `EntityDefinitionDescriptor.ExportDefinitionType` to the
   resolved `ExportDefinition`.
3. Whitelists scalar properties from `ExportDefinition.GetFields()`
   where `IsNavigation == false` and `PropertyPath` contains no `.`
   (flat scalar fields only for v1).
4. Keeps the existing `ExpandWhitelist` mechanism for navigation
   properties; future iterations derive these from
   `EntityDefinition.Relations` automatically.

Future iterations layer on top without breaking the contract:

- **Phase 1 follow-up** — overlay `RequiresPermission` from
  `EntityDefinition.Forms` / `Details` matching property path. Each
  module that ships an `EntityDefinition` automatically gains
  field-level perm gating on OData.
- **Phase 2** — derive `NavigationProperty` declarations from
  `EntityDefinition.Relations`, deprecate the manual
  `ExpandWhitelist`.
- **Phase 3** — `b.OData(...)` sugar (#1573) on `EntityDefinition`
  for cases where the OData surface diverges from Export.

## Consequences

### Positive

- **No more `DomainEvents` / `IntegrationEvents` leak.** Pinned by
  unit test on the EDM builder.
- **OData feed = state-of-progress indicator.** An entity needs an
  `EntityDefinition` to be exposed via BI; this aligns the OData
  surface with the framework's entity-modeling direction (Phase 2+
  will progressively add modules).
- **Single source of truth for field-level permissions** when the
  Phase 1 follow-up lands.
- **Single migration** instead of two.

### Negative

- **Showcase OData feed shrinks immediately.** Today (2026-05-01)
  only `Party` and `Invoice` have an `EntityDefinition` in
  `granit-dotnet`. The other 8 EntitySets currently wired in
  `granit-showcase-dotnet` (`Product`, `Tenant`, `PaymentTransaction`,
  `Subscription`, `BalanceAccount`, `BalanceTransaction`,
  `UsageAggregate`, `AuditEntry`) must either gain an
  `EntityDefinition` or leave the feed.
- **Adopters must declare an `EntityDefinition` before exposing an
  EntitySet.** This raises the bar — a deliberate design choice: if
  an entity is not yet ready for the framework's full declarative
  surface, it is not ready to be exposed to BI consumers either.
- **Strict-config error at startup** when an EntitySet has no
  matching `EntityDefinition`. Hosts that previously shipped without
  an `EntityDefinition` need to adapt. This is acceptable because:
  - The framework is pre-1.0 (CLAUDE.md global rule —
    breaking changes are clean, no `[Obsolete]` graduation).
  - The error message names the missing entity and the convention.

### Neutral

- The convention-based discovery is replaced by an explicit
  whitelist. Adopters lose the "everything public is exposed" model
  in exchange for explicit control. Aligns with the
  strict-config-validator philosophy already used elsewhere in the
  module (no silent defaults).

## Implementation notes

- `ODataEdmModelBuilder.Build(...)` consumes a per-entity field-set
  resolved at startup time. Field set comes from `ExportDefinition.GetFields()`
  with `IsNavigation == false` and `PropertyPath` containing no `.`.
- The CLR `Type` for each field is resolved via
  `entityType.GetProperty(propertyPath)?.PropertyType` at startup — the
  `ClrTypeName` string on `ExportFieldDescriptor` is informational.
- `ExpandWhitelist` properties continue to be declared as
  `NavigationProperty` via the convention builder's behaviour; only
  scalar property convention discovery is overridden.
- The strict-config validator is extended to: refuse a registered
  EntitySet whose entity has no `IEntityDefinitionRegistry.GetByEntityType`
  match, and refuse a registered EntitySet whose `EntityDefinition`
  does not declare a `b.Export<T>()` reference.

## References

- Reflection that drove this decision: [#1668](https://github.com/granit-fx/granit-dotnet/issues/1668)
- Sister story superseded: [#1575](https://github.com/granit-fx/granit-dotnet/issues/1575) — column whitelist via QueryDefinition (closed-as-superseded by this ADR)
- Long-term consolidation target: [#1584](https://github.com/granit-fx/granit-dotnet/issues/1584) — Granit.Entities-driven OData surface (this ADR is the v1 contract along that path)
- Sugar follow-up: [#1573](https://github.com/granit-fx/granit-dotnet/issues/1573) — `EntityDefinition.OData()` sugar
- Parent FEATURE: [#1369](https://github.com/granit-fx/granit-dotnet/issues/1369) — Layer 3: OData v4 connector for Power BI / Excel / Tableau
- ADR-020 — Query/Export/Metric pairing rule
- ADR-040 — Defense-in-depth on entity surface (field-level permissions)
- ADR-048 — Cross-module entity relations
