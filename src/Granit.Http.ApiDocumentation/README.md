# Granit.Http.ApiDocumentation

OpenAPI documentation and URL-based API versioning for Granit applications.
Generates one OpenAPI document per declared API version
(`Http:ApiDocumentation:MajorVersions`), registers `Asp.Versioning` with URL
segment and query string readers, applies the JWT Bearer / OAuth2 security
schemes and `[InternalApi]` endpoint filtering, and emits RFC 8594
deprecation headers (`Deprecation`, `Sunset`, `Link`) from
`DeprecatedAttribute` endpoint metadata alone.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Http.ApiDocumentation
```

## Usage

Call `app.MapGranitOpenApiDocuments()` in `Program.cs` to map the OpenAPI JSON
endpoints (`/openapi/v{n}.json`, one per major version):

```csharp
app.MapGranitOpenApiDocuments();
```

Without this call no documentation endpoints are exposed. For the interactive
Scalar UI, add the optional `Granit.Http.ApiDocumentation.Scalar` companion
package and call `app.UseGranitApiDocumentation()` instead — it maps the JSON
endpoints and layers the UI on top.

Mark an endpoint as deprecated (headers and `deprecated: true` in the document
come for free):

```csharp
app.MapGet("/api/v1/users", GetUsersAsync)
    .Deprecated(sunsetDate: new DateOnly(2026, 12, 31), link: "https://docs.example.com/migration");
```

## Dependencies

- `Granit`
- `Asp.Versioning.Mvc` + `Asp.Versioning.Mvc.ApiExplorer`
- `Granit.Http.ApiDocumentation.Scalar` (optional UI companion)

## Documentation

See the [full documentation](https://granit-fx.dev).
