# Granit.Documents.AssetMetadata.Endpoints

Minimal API endpoints for `Granit.Documents.AssetMetadata`. Reads the extracted
metadata row for a document's current version or for a specific version.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Documents.AssetMetadata.Endpoints
```

## Routes

| Method | Path | Permission | Purpose |
| --- | --- | --- | --- |
| `GET` | `/documents/{id}/metadata` | `Documents.Documents.Read` | Extracted metadata for the document's current version. |
| `GET` | `/documents/{id}/versions/{versionId}/metadata` | `Documents.Documents.Read` | Extracted metadata for a specific version. |

Both endpoints return 404 when the document is missing, excluded by the tenant
filter, or extraction has not yet produced a row for the requested version.

## Usage

```csharp
// Host registration
builder.AddGranitDocuments();
builder.AddGranitDocumentsAssetMetadata();
builder.AddGranitDocumentsEntityFrameworkCore(opts => opts.UseNpgsql(connString));
builder.AddGranitDocumentsAssetMetadataEntityFrameworkCore(opts => opts.UseNpgsql(connString));

// Map onto an existing /api group
app.MapGroup("/api")
   .MapGranitDocuments()
   .MapGranitDocumentsAssetMetadata();
```

## Response shape

Indexed-projection + raw archive: every well-known typed column (camera,
dimensions, GPS, page count, audio track …) is surfaced inline; the verbatim
extractor payload sits in `rawMetadata` under `{extractor}:{tag}` keys.

## Deferred — write surface

The HTTP layer is read-only. Extraction is event-driven (F17.4 background job
on `DocumentVersionAddedEvent`); manual re-extraction endpoints are out of
scope for the first cut.
