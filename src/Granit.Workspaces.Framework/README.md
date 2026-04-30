# Granit.Workspaces.Framework

Framework navigation shell
([ADR-040 §7](https://granit-fx.dev/dotnet/architecture/adr/040-entity-definition/)) —
declares the `Granit.Framework` root workspace plus 8 shell sub-workspaces
(`System` / `Users` / `Automation` / `Data` / `Email` / `Integrations` /
`Monitoring` / `Privacy`) populated by other modules through
`IWorkspaceContributor`.

Zero business-module dependency. Empty shells auto-filter from the rendered
tree (handled by the workspace composer in `Granit.Workspaces`). Hidden from
non-admin users via the `Workspace.Granit.Framework.Read` permission gate.

Pull this from a host that wants the framework navigation:

```csharp
[DependsOn(
    typeof(GranitWorkspacesEndpointsModule),
    typeof(GranitWorkspacesFrameworkModule))]
public sealed class HostModule : GranitModule;
```

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Workspaces.Framework
```

## Dependencies

- `Granit.Localization`
- `Granit.Workspaces.Abstractions`
