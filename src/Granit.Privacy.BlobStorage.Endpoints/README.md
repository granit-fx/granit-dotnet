# Granit.Privacy.BlobStorage.Endpoints

HTTP surface for the blob-backed privacy export download.
`MapGranitPrivacyExportDownload()` maps `GET /{prefix}/exports/{requestId}/download`,
resolves the caller's export request, and 302-redirects to a presigned URL for the
archive manifest produced by the export assembly job.

Split from `Granit.Privacy.BlobStorage` so the data-plumbing package carries no
`Microsoft.AspNetCore.App` framework reference (layer purity), and from
`Granit.Privacy.Endpoints` so the core privacy endpoints (opt-out, deletion,
agreements, purposes, regulation) stay usable without pulling BlobStorage into
every host.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Privacy.BlobStorage.Endpoints
```

```csharp
api.MapGranitPrivacyExportDownload();
```

## Dependencies

- `Granit.BlobStorage`
- `Granit.Privacy`
- `Granit.Validation`

## Documentation

See the [full documentation](https://granit-fx.dev).
