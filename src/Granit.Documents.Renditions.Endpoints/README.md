# Granit.Documents.Renditions.Endpoints

Minimal API endpoints for `Granit.Documents.Renditions`. Lists renditions and
issues presigned download URLs for `Ready` ones.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Documents.Renditions.Endpoints
```

## Routes

| Method | Path | Permission | Purpose |
| --- | --- | --- | --- |
| `GET` | `/documents/{id}/renditions` | `Documents.Documents.Read` | Lists every rendition row for the document's current version (any status). |
| `GET` | `/documents/{id}/renditions/{type}/download` | `Documents.Documents.Read` | Returns a presigned URL for a `Ready` rendition. Pass `?format=image/webp` to disambiguate when several MIMEs are ready. |

## Usage

```csharp
// Host registration
builder.AddGranitDocuments();
builder.AddGranitDocumentsRenditions();
builder.AddGranitDocumentsEntityFrameworkCore(opts => opts.UseNpgsql(connString));
builder.AddGranitDocumentsRenditionsEntityFrameworkCore(opts => opts.UseNpgsql(connString));

// Map onto an existing /api group
app.MapGroup("/api")
   .MapGranitDocuments()
   .MapGranitDocumentsRenditions();
```

## Deferred — on-demand fallback

Running the rendition pipeline synchronously at download time for a missing
rendition isn't part of this slice. Callers retry once the F16.4 background
job completes (it generates the rendition set on `DocumentVersionAddedEvent`).
