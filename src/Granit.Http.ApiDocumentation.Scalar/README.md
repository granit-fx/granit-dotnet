# Granit.Http.ApiDocumentation.Scalar

Scalar interactive API reference UI for Granit applications. Optional UI
companion to `Granit.Http.ApiDocumentation`: hosts the Scalar UI on top of the
generated OpenAPI documents, relaxes the Content-Security-Policy on the Scalar
route only, and supports the OAuth2 Authorization Code popup flow (PKCE).

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Http.ApiDocumentation.Scalar
```

## Usage

Call `app.UseGranitApiDocumentation()` in `Program.cs` (after
`UseAuthorization`) to map the OpenAPI JSON endpoints (`/openapi/v{n}.json`)
and the Scalar UI:

```csharp
app.UseGranitApiDocumentation();
```

Enabled in Development by default; opt into production exposure with
`Http:ApiDocumentation:Scalar:EnableInProduction` (and set
`Http:ApiDocumentation:Scalar:AuthorizationPolicy` when you do).

UI-only settings live under `Http:ApiDocumentation:Scalar` (favicon,
production gate, authorization policy, OAuth2 client id / PKCE / redirect
URI). Document-generation settings — versions, title, OAuth2 endpoint URLs
and scopes — stay in `Http:ApiDocumentation`.

Headless hosts (contract generators, machine-to-machine APIs) do not need
this package: `app.MapGranitOpenApiDocuments()` from
`Granit.Http.ApiDocumentation` serves the JSON documents without any UI stack.

## Dependencies

- `Granit.Http.ApiDocumentation`
- `Granit.Http.SecurityHeaders.Abstractions`
- `Scalar.AspNetCore`

## Documentation

See the [full documentation](https://granit-fx.dev).
