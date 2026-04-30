# Granit.Entities

Runtime services for the Granit entity manifest: the
`IEntityDefinitionRegistry` and the boot-time integrity-check runner that
validates every reference cited by an `EntityDefinition` (`QueryDefinition`,
`ExportDefinition`, `MetricDefinition`, `DashboardDefinition`,
`IWorkflowDefinition`) is resolvable through DI.

Pull this package from a host that resolves the manifest. Pull
`Granit.Entities.Abstractions` alone from any base module that only **declares**
an `EntityDefinition` — that way the base module stays free of the runtime
registry, hosted service, and DI introspection.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Entities
```

## Dependencies

- `Granit.Entities.Abstractions`
- `Microsoft.Extensions.Hosting.Abstractions`

## Documentation

See the [full documentation](https://granit-fx.dev) and
[ADR-040](https://granit-fx.dev/dotnet/architecture/adr/040-three-tier-metadata-architecture/)
for the three-tier metadata architecture.
