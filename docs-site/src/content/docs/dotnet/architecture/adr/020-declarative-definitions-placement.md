---
title: "ADR-020: Declarative Definitions Placement (Query & Export)"
description: "Concrete *QueryDefinition and *ExportDefinition classes live in the base module alongside the domain, not in the .Endpoints HTTP layer. Each module owns its definitions; no central aggregation package."
sidebar:
  order: 20
  label: "020 - Declarative Definitions Placement"
---

> **Date:** 2026-04-18
> **Authors:** Jean-Francois Meyers
> **Scope:** granit-dotnet (Granit.QueryEngine, Granit.DataExchange, all modules with admin-visible entities)

## Context

The framework provides two declarative primitives that describe how an admin-visible
entity is consulted and exported:

- `QueryDefinition<TEntity>` (in `Granit.QueryEngine`) — declares filterable/sortable
  columns, quick filters, group-by fields, default pagination. Consumed by
  `MapGranitQueryEndpoint<T>()` to expose a paginated grid endpoint, and by
  `ExportOrchestrator` to apply the same filtering pipeline during export.
- `ExportDefinition<TEntity>` (in `Granit.DataExchange`) — declares a security-by-design
  whitelist of exportable fields with headers and formats. Consumed by the export
  pipeline to produce CSV / XLSX outputs.

Both are **pure declarations**: no HTTP, no DbContext, no runtime dependency. Yet their
placement across the codebase had drifted into two inconsistent patterns:

- **`QueryDefinition`s** were scattered across `Granit.{Module}.Endpoints/Queries/`,
  forcing consumers (background jobs, export orchestrator, tests) to pull the entire
  HTTP layer just to reuse a query contract.
- **`ExportDefinition`s** were aggregated into a single `Granit.DataExchange.Definitions`
  package that referenced 23 framework modules — a "fat aggregation" anti-pattern that
  created a god-package and prevented modules from owning their own admin contract.

Compounding the issue, definitions were created ad hoc — some entities had a
`QueryDefinition` but no `ExportDefinition` (or vice versa), leaving the admin UX
half-implemented (browse without export, or export without filtering).

## Decision

### 1. Placement — base module, not `.Endpoints`

Concrete `*QueryDefinition` and `*ExportDefinition` classes live in the **base module**
(`Granit.{Module}`), in dedicated `Queries/` and `Exports/` folders:

```text
Granit.Invoicing/
  Domain/
    Invoice.cs
  Queries/
    InvoiceQueryDefinition.cs       ← consultation contract
  Exports/
    InvoiceExportDefinition.cs      ← export contract
  GranitInvoicingModule.cs          ← registers both
```

The `.Endpoints` package is reserved for HTTP-layer artifacts: route groups,
request/response DTOs, validators, permission providers.

### 2. Base classes in `.Abstractions`

The declarative primitives themselves live in lightweight `.Abstractions` packages so
domain modules can reference them without pulling the full runtime:

| Primitive | Base class | Runtime |
| --------- | ---------- | ------- |
| `QueryDefinition<T>`, builders, descriptors | `Granit.QueryEngine.Abstractions` | `Granit.QueryEngine`, `Granit.QueryEngine.EntityFrameworkCore`, `Granit.QueryEngine.AspNetCore` |
| `ExportDefinition<T>`, builders, descriptors | `Granit.DataExchange.Abstractions` (new) | `Granit.DataExchange` (orchestrator, writers, jobs) |

A base module declaring definitions adds two `<ProjectReference>` entries — both to
`.Abstractions` packages, both lightweight. The full engine/orchestrator is only pulled
in by hosts that actually execute queries and exports.

### 3. Each module owns its registrations

Definitions are registered in the module's `ConfigureServices`:

```csharp
public sealed class GranitInvoicingModule : GranitModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddQueryDefinition<Invoice, InvoiceQueryDefinition>();
        context.Services.AddExportDefinition<Invoice, InvoiceExportDefinition>();
    }
}
```

No central `Granit.{X}.Definitions` package exists. The previous
`Granit.DataExchange.Definitions` is **deleted**.

### 4. Pairing — every admin-visible entity has both

For every admin-visible aggregate root or entity (i.e., one exposed in an admin grid),
the module MUST provide **both** a `QueryDefinition` AND an `ExportDefinition`.
Architecture tests enforce this pairing.

Pure infrastructure entities (internal cache rows, audit log details, internal config
state) are exempt and use the reflection-based fallback `ReflectionExportDefinition`.

## Evaluated alternatives

### Alternative A — keep definitions in `.Endpoints`

**Rejected.** Couples a pure declaration to the HTTP layer, makes definitions
inaccessible to non-HTTP consumers (background jobs, tests), misnames the package
(`.Endpoints` should hold only HTTP artifacts).

### Alternative B — definitions in `.EntityFrameworkCore`

**Rejected.** Couples the declaration to the persistence layer. Admin contracts should
not depend on the choice of ORM. A future `Granit.{Module}.Marten` (event-sourced)
should be able to reuse the same definitions.

### Alternative C — dedicated `Granit.{Module}.Queries` / `.Exports` packages

**Rejected.** Doubles the package count (128 → 250+) for a benefit that doesn't
materialize: nothing consumes a definition without also consuming the rest of the base
module's contract.

### Alternative D — central `Granit.DataExchange.Definitions` (status quo for exports)

**Rejected.** Creates a god-package depending on every framework module, prevents
ownership, and forces all CI shards to rebuild on any framework change. The package was
deleted as part of this ADR.

## Justification

- **Layering correctness**: declarations belong with the domain they describe, not with
  the HTTP transport that happens to expose them today.
- **Consumer parity**: HTTP endpoints, background jobs (export orchestrator), Wolverine
  handlers, and integration tests all consume the same definition. None should require
  pulling `.Endpoints`.
- **Module ownership**: each module owns the full contract for its entities — domain,
  events, queries, exports, validators (in `.Endpoints`), permissions (in `.Endpoints`).
  No external package is required to "register" a module's admin surface.
- **Symmetry**: queries and exports are two facets of the same use case. Pairing them
  enforces a consistent admin UX (no half-implemented entities).
- **Lightweight `.Abstractions`**: the dependency cost on base modules is negligible —
  `Granit.QueryEngine.Abstractions` and `Granit.DataExchange.Abstractions` carry only
  contracts and builders, no runtime.

## Consequences

### Positive

- Base modules become self-contained admin contracts (domain + queries + exports).
- `Granit.DataExchange.Definitions` deleted (-1 package, -1 fat dependency graph).
- Definitions are reachable from any consumer with a single `.Abstractions` reference.
- Architecture tests enforce the Query/Export pairing — no half-implemented entities.

### Negative

- One-time migration cost: every existing `*QueryDefinition.cs` moves from
  `.Endpoints/Queries/` to base module `Queries/`; every existing `*ExportDefinition.cs`
  moves from `Granit.DataExchange.Definitions/{Module}/` to base module `Exports/`.
- Each base module that declares definitions adds two `<ProjectReference>` lines (to
  `Granit.QueryEngine.Abstractions` and `Granit.DataExchange.Abstractions`).
- Some entities currently lacking a definition need one created (gap-filling pass).
- Localization keys (`Column:…`, `ExportHeader:…`) move from `.Endpoints` localization
  resources to base-module localization resources.

### Migration order

1. Move `QueryDefinition<T>` (base class), `QueryDefinitionBuilder<T>`, column/filter
   builders and descriptors from `Granit.QueryEngine` → `Granit.QueryEngine.Abstractions`.
2. Create `Granit.DataExchange.Abstractions` with `ExportDefinition<T>` and supporting
   builders/descriptors.
3. Move existing `*QueryDefinition.cs` files from `.Endpoints/Queries/` to
   `Granit.{Module}/Queries/`.
4. Distribute existing `*ExportDefinition.cs` files from
   `Granit.DataExchange.Definitions/{Module}/` to `Granit.{Module}/Exports/`.
5. Delete `Granit.DataExchange.Definitions` (and its `.Tests` project).
6. Create missing definitions for unpaired entities (Query without Export, or vice
   versa).
7. Add architecture test enforcing the Query/Export pairing rule.

## References

- [`CLAUDE.md` — Declarative definitions section](../../../../../../../CLAUDE.md)
- ADR-015 — [Sep CSV Parsing](./015-sep-csv-parsing.md)
- ADR-016 — [Sylvan.Data.Excel Parsing](./016-sylvan-data-excel-parsing.md)
- ADR-017 — [DDD Aggregate Root & Value Object Strategy](./017-ddd-aggregate-value-object-strategy.md)
