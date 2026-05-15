# Granit.Workspaces.Abstractions

Inter-module contracts for the Granit workspace surface
([ADR-057](https://granit-fx.dev/dotnet/architecture/adr/057-workspace-composition-belongs-to-the-application/)) —
declarative `WorkspaceDefinition` base class, fluent builder, section / item
descriptors, and the `IFeatureProvider` module-side feature catalog hook.

Pure abstractions, no runtime — pull this from any base module that exposes
features or declares a `WorkspaceDefinition`. Pull `Granit.Workspaces` only
from hosts that resolve and serve the tree.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Workspaces.Abstractions
```

## Dependencies

- `Granit`
- `Granit.Entities.Abstractions`
