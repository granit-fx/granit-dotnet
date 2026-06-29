# Granit.Wolverine

WolverineFx integration for Granit. Transactional outbox, ISO 27001 context propagation, and distributed tracing.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Wolverine
```

## Dependencies

- `Granit.Users`
- `Granit.Validation`

## Usage

### Module registration

Wolverine is wired by the Granit module system — there is no explicit `Program.cs`
call. `GranitWolverineModule.ConfigureServices` calls
`AddGranitWolverine(context.ModuleAssemblies)` for you, so adding any module that
depends on `GranitWolverineModule` (for example `GranitEventsWolverineModule`, pulled
in via `[DependsOn]`) wires it transitively.

For durable messaging / outbox, also add a transport provider module such as
`GranitWolverinePostgresqlModule` after this one.

### Handler discovery

Wolverine discovers message handlers by convention (handler classes and methods)
scanned from the assemblies passed in `context.ModuleAssemblies` — no attribute is
needed. (There is no `[MessageHandler]` attribute in the framework.)

## Documentation

See the [full documentation](https://granit-fx.dev).
