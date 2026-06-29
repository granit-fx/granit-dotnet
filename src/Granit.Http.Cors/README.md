# Granit.Http.Cors

Standardized CORS configuration for Granit applications. ISO 27001-compliant:
wildcard origins blocked in production, validated `AllowCredentials` usage.
Configurable via `appsettings.json`.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Http.Cors
```

## Configuration

The default policy is bound from the `Http:Cors` section of `appsettings.json`:

```json
{
  "Http": {
    "Cors": {
      "AllowedOrigins": ["https://app.example.com"],
      "AllowCredentials": true
    }
  }
}
```

`AllowedOrigins` is required (at least one origin); the wildcard `*` is rejected
outside Development (ISO 27001), and `AllowCredentials: true` cannot be combined
with a wildcard origin.

## Usage

Two `Program.cs` steps activate CORS:

1. `builder.AddGranitCors()` registers the default policy from the `Http:Cors`
   config section.
2. `app.UseCors()` adds the middleware that applies it — without this the
   registered policy is inert and no CORS headers are emitted.

`app.UseCors()` must run before the authentication/authorization middleware; its
exact placement relative to other middleware (e.g. response compression) is
application-specific.

## Dependencies

- `Granit`

## Documentation

See the [full documentation](https://granit-fx.dev).
