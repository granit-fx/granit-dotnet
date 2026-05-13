# Granit.Entities.Abstractions

Inter-module contracts for the Granit entity manifest: the declarative
`EntityDefinition<TEntity>` base class, fluent `EntityDefinitionBuilder<TEntity>`,
form / detail / side-panel descriptors, and the closed-enum visibility DSL
(`FieldOp`, `VisibilityCondition`).

Reference this package from any base module that **declares** an entity's UI
surface. Reference `Granit.Entities` only from hosts that **execute** the
integrity check or **serve** the manifest.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Entities.Abstractions
```

## Dependencies

- `Granit`
- `Granit.DataExchange.Abstractions`
- `Granit.QueryEngine.Abstractions`
- `Granit.Workflow.Abstractions`

## Documentation

See the [full documentation](https://granit-fx.dev) and
[ADR-040](https://granit-fx.dev/dotnet/architecture/adr/040-three-tier-metadata-architecture/)
for the three-tier metadata architecture.
