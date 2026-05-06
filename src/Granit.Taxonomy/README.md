# Granit.Taxonomy

Cross-cutting taxonomy module for the Granit framework — scoped tags and hierarchical
categories assignable to any aggregate root via a polymorphic `TagAssignment` /
`CategoryAssignment` table. One physical table with a `Scope` discriminator keeps
per-domain autocomplete focused (Documents, Parties, Activities, …) while enabling
single-query cross-entity search. Hosts that prefer a single global pot collapse all
scopes to `"global"`.

This base package contains the abstractions: the `Tag` aggregate, `ITagService`,
domain events, options, diagnostics (`TaxonomyMetrics`, `TaxonomyActivitySource`).
Persistence ships in `Granit.Taxonomy.EntityFrameworkCore`, HTTP endpoints in
`Granit.Taxonomy.Endpoints`.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Taxonomy
```

## Dependencies

- `Granit`
- `Granit.Persistence`

## Documentation

See [ADR-054](../../docs-site/src/content/docs/dotnet/architecture/adr/054-taxonomy-module.md)
for the architecture decisions (scoped-vs-pot trade-off, polymorphic FK with
discriminator + indexes, Tag aggregate shape, phasing) and the
[full documentation](https://granit-fx.dev).
