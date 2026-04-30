# Granit.Workspaces.Abstractions

Inter-module contracts for the Granit workspace surface
([ADR-040](https://granit-fx.dev/dotnet/architecture/adr/040-entity-definition/)) —
declarative `WorkspaceDefinition` base class, fluent builder, section / item
descriptors, and the `IWorkspaceContributor` cross-module grafting hook.

Pure abstractions, no runtime — pull this from any base module that declares
a `WorkspaceDefinition` or contributes to one. Pull `Granit.Workspaces` only
from hosts that resolve and serve the tree.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Workspaces.Abstractions
```

## Dependencies

- `Granit`
- `Granit.Entities.Abstractions`
