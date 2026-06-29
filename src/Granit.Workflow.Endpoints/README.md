# Granit.Workflow.Endpoints

Minimal API endpoints for `Granit.Workflow`. Exposes transition history REST
endpoint for frontend integration and ISO 27001 audit trail querying.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Workflow.Endpoints
```

## Dependencies

- `Granit.Http.ApiDocumentation`
- `Granit.Authorization`
- `Granit.Validation`
- `Granit.Workflow`

## Usage

The module calls `AddGranitWorkflowEndpoints()` automatically once referenced.
The only manual step is mapping the routes — call `MapGranitWorkflow()` on your
API route group (after `UseAuthorization()`):

```csharp
api.MapGranitWorkflow();
```

Without this call the transition-history endpoints are never exposed.

## Documentation

See the [full documentation](https://granit-fx.dev).
