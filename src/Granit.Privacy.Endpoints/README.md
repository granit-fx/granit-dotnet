# Granit.Privacy.Endpoints

Minimal API endpoints for GDPR data subject rights: personal data export (Art. 15/20), data deletion (Art. 17), and legal agreement consent management (Art. 7). Backed by Granit.Privacy scatter-gather saga, deletion orchestration, and consent versioning.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Privacy.Endpoints
```

## Usage

Map the endpoints this package owns during endpoint registration:

```csharp
// Authenticated regulation / export / deletion / agreements endpoints,
// under the configurable privacy prefix:
api.MapGranitPrivacy();

// Optional anonymous Global Privacy Control discovery resource —
// RFC 8615 /.well-known/gpc.json, mounted at the host root, outside the
// versioning prefix and excluded from OpenAPI:
app.MapGranitPrivacyGpcDiscovery();
```

The export-download endpoint (`MapGranitPrivacyExportDownload`) is provided by
the separate `Granit.Privacy.BlobStorage.Endpoints` package — see that module's
README.

## Dependencies

- `Granit.Authorization`
- `Granit.Guids`
- `Granit.Http.ApiDocumentation`
- `Granit.Http.Cookies`
- `Granit.Privacy`
- `Granit.Privacy.Regulations`
- `Granit.Validation`
- `Granit.Workspaces.Abstractions`

## Documentation

See the [full documentation](https://granit-fx.dev).
