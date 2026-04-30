# Granit.Workspaces.Endpoints

Minimal API endpoint surfacing the Granit workspace tree
([ADR-040](https://granit-fx.dev/dotnet/architecture/adr/040-entity-definition/)).

`GET /api/workspaces` returns the tree filtered by user permissions, with
empty workspaces / sections / items dropped from the payload (defense in
depth, never just hidden). Pass `?includeShells=false` to omit Framework
shells. FusionCache 5 min sliding per (user-perms-hash, culture, includeShells).

Mount the route from the host pipeline:

```csharp
app.MapGranitWorkspacesEndpoints("/api/v1/workspaces");
```

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Workspaces.Endpoints
```

## Dependencies

- `Granit.Authorization`
- `Granit.Caching`
- `Granit.Http.ApiDocumentation`
- `Granit.Localization`
- `Granit.Validation`
- `Granit.Workspaces`
