# Granit.Http.ApiDocumentation

OpenAPI documentation and Scalar UI for Granit applications. Generates one OpenAPI document per declared API version, with JWT Bearer security scheme and endpoint filtering.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Http.ApiDocumentation
```

## Usage

Call `app.UseGranitApiDocumentation()` in `Program.cs` (after `UseAuthorization`)
to map the OpenAPI JSON endpoints (`/openapi/v{n}.json`) and the Scalar UI:

```csharp
app.UseGranitApiDocumentation();
```

Without this call no documentation endpoints are exposed.

## Dependencies

- `Granit.Http.ApiVersioning`
- `Granit.Users`

## Documentation

See the [full documentation](https://granit-fx.dev).
