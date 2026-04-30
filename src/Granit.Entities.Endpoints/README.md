# Granit.Entities.Endpoints

Minimal API endpoints for the Granit entity manifest
([ADR-040](https://granit-fx.dev/dotnet/architecture/adr/040-entity-definition/)) —
discovery (`GET /api/entities`) plus the per-entity manifest
(`GET /api/entities/{name}`) that aggregates identity, permissions, forms,
details, collections, dashboards, exports and views into one payload.

Defense-in-depth filtering: fields, sections and side panels gated by a
permission the caller does not hold are absent from the payload, never just
hidden. FusionCache + strong ETag, 18-culture localisation.

Mount the routes from the host pipeline:

```csharp
app.MapGranitEntitiesEndpoints("/api/v1/entities");
```

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Entities.Endpoints
```

## Dependencies

- `Granit.Authorization`
- `Granit.Caching`
- `Granit.Entities`
- `Granit.Http.ApiDocumentation`
- `Granit.Localization`
- `Granit.Validation`
