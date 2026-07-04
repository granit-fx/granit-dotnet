# Granit.Workflow

Generic finite state machine (FSM) engine for entity lifecycle management. Fluent
API for workflow definitions, multi-step approval routing, domain events, and
versioned entities with draft/published/archived lifecycle.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Workflow
```

## Dependencies

- `Granit.QueryEngine`
- `Granit.Timing`

## Core usage

Registration is two steps:

```csharp
// 1. Register core services once (permission checker, metrics,
//    transition-record query/export definitions).
services.AddGranitWorkflow();

// 2. Register each workflow definition (singleton) and its
//    IWorkflowManager<TState> (scoped).
services.AddWorkflow<MyState>(definition);
```

`MyState` is a `struct, Enum` and `definition` is an
`IWorkflowDefinition<MyState>` — build it with the fluent
`WorkflowDefinitionBuilder<TState>` from `Granit.Workflow.Abstractions`. Without
`AddWorkflow<TState>()`, no concrete state machine is available at runtime.

## Several entities sharing one state enum

When two entities share the same `TState` enum but need distinct,
permission-gated definitions (e.g. `BlogPost` and `CmsPage` both on
`WorkflowLifecycleStatus`), register each definition **keyed** by its
`IWorkflowStateful.WorkflowEntityType`:

```csharp
services.AddWorkflow("BlogPost", blogDefinition);
services.AddWorkflow("CmsPage", pageDefinition);
```

Resolve the right manager per entity through `IWorkflowManagerFactory`:

```csharp
IWorkflowManager<WorkflowLifecycleStatus> manager =
    factory.GetManager<WorkflowLifecycleStatus>("BlogPost");
// or, by entity type:
IWorkflowManager<WorkflowLifecycleStatus> byEntity =
    factory.GetManager<BlogPost, WorkflowLifecycleStatus>();
```

or by injecting `[FromKeyedServices("BlogPost")] IWorkflowManager<TState>`. The
non-keyed `AddWorkflow<TState>(definition)` overload keeps working unchanged and
is also reachable via the factory under `typeof(TState).FullName`.

## Documentation

See the [full documentation](https://granit-fx.dev).
