# Granit.Workflow.Abstractions

Lightweight contracts for the Granit.Workflow finite state machine engine:
`IWorkflowDefinition<TState>`, `WorkflowDefinition<TState>`, the fluent
`WorkflowDefinitionBuilder<TState>` and `TransitionBuilder<TState>`, and the
ambient `WorkflowTransitionContext`. Reference this package from any base
module that **declares** a workflow shape. Reference `Granit.Workflow` only
from hosts that **execute** transitions (orchestration, persistence, events).

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Workflow.Abstractions
```

## Dependencies

- `Granit`

## Documentation

See the [full documentation](https://granit-fx.dev).
