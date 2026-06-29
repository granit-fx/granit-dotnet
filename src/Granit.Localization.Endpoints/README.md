# Granit.Localization.Endpoints

Minimal API endpoints for Granit localization: anonymous GET for SPA bootstrapping and admin CRUD for translation overrides.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Localization.Endpoints
```

## Dependencies

- `Granit.Http.ApiDocumentation`
- `Granit.Authorization`
- `Granit.Localization`
- `Granit.Validation`

## Integration

### 1. Add the module

In your host's module, declare the dependency:

```csharp
[DependsOn(typeof(GranitLocalizationEndpointsModule))]
public sealed class YourHostModule : GranitModule;
```

### 2. Map endpoints

In `Program.cs`, after creating the route group, map the localization endpoints:

```csharp
var api = app.MapGroup("api/v{version:apiVersion}");
api.MapGranitLocalization();
api.MapGranitLocalizationOverrides();
```

Without these calls, the `GET /api/{version}/localization` and CRUD
`/api/{version}/localization/overrides` endpoints are not registered.

## Documentation

See the [full documentation](https://granit-fx.dev).
