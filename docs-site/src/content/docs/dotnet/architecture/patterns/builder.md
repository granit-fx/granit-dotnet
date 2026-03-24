---
title: "Builder Pattern — Fluent Configuration in .NET"
description: "Compose complex module configuration step-by-step with fluent AddGranit*() extension methods — readable DI registration without bloated constructors or config files."
sidebar:
  label: Builder
  order: 28
---

## Definition

The Builder pattern separates the construction of a complex object from its
representation, enabling different configurations via a fluent interface. In
Granit, this pattern manifests in the `AddGranit*()` extension methods that
configure services module by module.

## Diagram

```mermaid
sequenceDiagram
    participant App as Program.cs
    participant Ext as AddGranitWolverine()
    participant Opts as WolverineMessagingOptions
    participant DI as IServiceCollection
    participant Val as ValidateOnStart

    App->>Ext: builder.AddGranitWolverine(configure?)
    Ext->>Opts: AddOptions().BindConfiguration("Wolverine")
    Ext->>Val: ValidateDataAnnotations().ValidateOnStart()
    Ext->>DI: AddScoped of ICurrentUserService
    Ext->>DI: AddSingleton of WolverineActivitySource
    opt configure provided
        Ext->>Opts: configure.Invoke(options)
    end
    Ext-->>App: IHostApplicationBuilder (fluent)
```

## Implementation in Granit

Each module exposes an `AddGranit*()` extension method:

| Extension | File | Receiver |
|-----------|------|----------|
| `AddGranit<TModule>()` | `src/Granit/Extensions/GranitHostBuilderExtensions.cs` | `IHostApplicationBuilder` |
| `AddGranitWolverine()` | `src/Granit.Wolverine/Extensions/WolverineHostApplicationBuilderExtensions.cs` | `IHostApplicationBuilder` |
| `AddGranitBackgroundJobs()` | `src/Granit.BackgroundJobs/Extensions/BackgroundJobsHostApplicationBuilderExtensions.cs` | `IHostApplicationBuilder` |
| `AddGranitFeatures()` | `src/Granit.Features/ServiceCollectionExtensions.cs` | `IServiceCollection` |
| `AddGranitLocalization()` | `src/Granit.Localization/Extensions/LocalizationServiceCollectionExtensions.cs` | `IServiceCollection` |

**Audit note**: the signatures are not yet symmetric across modules (see
finding C2 in the critical dashboard). The target is the
`AddOptions<T>().BindConfiguration().ValidateOnStart()` pattern.

## Rationale

The Builder allows each NuGet package to self-configure without the host
application needing to know internal details. A single call replaces dozens
of DI registration lines.

## Usage example

```csharp
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// One call per module -- fluent and composable
builder.AddGranit<MyAppHostModule>();
// Internally, the ModuleLoader calls AddGranitWolverine(),
// AddGranitPersistence(), AddGranitFeatures(), etc.
// in topological dependency order
```

## Further reading

- [Builder -- refactoring.guru](https://refactoring.guru/design-patterns/builder)
