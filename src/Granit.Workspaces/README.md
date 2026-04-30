# Granit.Workspaces

Granit workspace runtime
([ADR-040](https://granit-fx.dev/dotnet/architecture/adr/040-entity-definition/))
— hosts the `IWorkspaceRegistry` built at boot from every registered
`WorkspaceDefinition` merged with cross-module `IWorkspaceContributor` grafts.

Empty shells are auto-filtered, depth is capped at 4 (per ADR-040 §7) and
contributions targeting unknown workspaces are dropped silently with a debug
log — module load order is non-deterministic.

Pull this from a host that resolves the workspace tree; pull
`Granit.Workspaces.Abstractions` alone from any base module that only declares
or contributes to workspaces.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Workspaces
```

## Dependencies

- `Granit.Workspaces.Abstractions`
