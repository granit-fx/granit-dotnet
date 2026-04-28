# Granit.Dashboards.Endpoints

Minimal API endpoints for `Granit.Dashboards`. Today ships the read-only
catalogue endpoint:

```text
GET /{prefix}/catalog?category={category}
```

…surfacing every registered `DashboardDefinition` through the
`IDashboardDefinitionRegistry`. Each entry carries the wire identifier, category,
version, widget count, and feature flags (`HasViews` / `HasAliases` /
`HasFilters`) the frontend uses to drive the import dialog.

Permission gating: `Dashboards.Catalog.Read`. Localized in all 18 cultures.

The import / CRUD endpoints land on the same route group in subsequent stories.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Dashboards.Endpoints
```

```csharp
app.MapGranitDashboards(options => options.RoutePrefix = "dashboards");
```

## Dependencies

- `Granit.Dashboards`
- `Granit.Authorization`
- `Granit.Http.Abstractions`
- `Granit.Validation`

## Documentation

See the [full documentation](https://granit-fx.dev).
