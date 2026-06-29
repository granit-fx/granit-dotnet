# Granit.Http.ApiVersioning

URL-based API versioning for Granit applications. Registers Asp.Versioning with URL and query string readers.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Http.ApiVersioning
```

## Usage

Versioning is wired in two layers.

The module registers Asp.Versioning via `AddGranitApiVersioning()` (run by
`GranitHttpApiVersioningModule`), configurable through `GranitApiVersioningOptions`
(`DefaultMajorVersion`, `ReportApiVersions`).

At the app layer, build a version set and apply it to a route group:

```csharp
ApiVersionSet set = app.NewApiVersionSet()
    .HasApiVersion(new ApiVersion(1))
    .ReportApiVersions()
    .Build();

RouteGroupBuilder api = app.MapGroup("api/v{version:apiVersion}")
    .WithApiVersionSet(set);
```

The version is read from the URL segment, with a query-string fallback reader
(`?api-version=1.0`).

## Dependencies

- `Granit`

## Documentation

See the [full documentation](https://granit-fx.dev).
