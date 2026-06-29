# Granit.Templating.Endpoints

Minimal API admin endpoints for managing Scriban templates: CRUD (draft lifecycle),
publish/unpublish, and revision history. All endpoints require the `Templates.Manage` permission
and are protected by Keycloak-based RBAC via `Granit.Authorization`.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Templating.Endpoints
```

## Dependencies

- `Granit.Http.ApiDocumentation`
- `Granit.Authorization`
- `Granit.QueryEngine.AspNetCore`
- `Granit.Templating`
- `Granit.Validation`
- `Granit.Workspaces.Abstractions`

## Documentation

See the [full documentation](https://granit-fx.dev).
