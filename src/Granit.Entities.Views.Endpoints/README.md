# Granit.Entities.Views.Endpoints

Minimal API endpoints for the Granit `EntityView` surface
([ADR-047](https://granit-fx.dev/dotnet/architecture/adr/047-entity-view/)) — list /
create / read / update / delete plus the four state-transition routes
(pin / star / set-default / share).

Permissions follow the closed `EntityViewPermissions` set
(`Read` / `Create` / `Share` / `Manage` / `Delete.Any`); FluentValidation gates
request DTOs; localisation across the 18 cultures.

Mount the routes from the host pipeline:

```csharp
app.MapGranitEntityViewsEndpoints("/api/v1/entities");
```

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Entities.Views.Endpoints
```

## Dependencies

- `Granit.Authorization`
- `Granit.Entities.Views`
- `Granit.Http.ApiDocumentation`
- `Granit.Validation`
