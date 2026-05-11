# Granit.Documents.AssetMetadata

Asset-metadata extraction abstraction for `Granit.Documents` (F17). Provider
pattern over EXIF / IPTC / XMP, PDF info-dictionary, OOXML core properties, and
ID3 / Vorbis / MP4 container metadata.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Documents.AssetMetadata
```

## Storage model

`Granit.Documents.AssetMetadata` uses the **indexed-projection + raw-archive**
pattern that Cloudinary, Bynder, and Adobe AEM Assets converged on:

- Well-known fields (`Width`, `Height`, `CameraMake`, `Title`, `PageCount`,
  `DurationMs`, `Artist`, …) lift to typed columns on the
  `DocumentAssetMetadata` aggregate. SQL queries filter on them directly —
  gallery by camera, range query by capture date, GPS bounding box, etc.
- The full extractor payload lands in a `RawMetadata` dictionary persisted as
  `jsonb` on Postgres and `nvarchar(max)` on SQL Server / SQLite (see
  `Granit.Documents.AssetMetadata.EntityFrameworkCore`). Full fidelity for
  audit, forensics, and future schema migrations without re-extracting.

## What this package ships

- `DocumentAssetMetadata` aggregate (1:1 with `DocumentVersion`) with the typed
  columns + `RawMetadata` dictionary.
- `IAssetMetadataExtractor` — provider contract; each extractor reports the
  MIME family it handles, projects its findings into typed fields, appends its
  raw output under a prefix `{extractorName}:`.
- `IAssetMetadataPipeline` — runs every applicable extractor in sequence,
  merging by first-write-wins on typed columns.
- `IAssetMetadataStore` — storage contract (EFC impl lives in the companion).
- `GranitAssetMetadataOptions` — most notably `StripGpsOnUpload` (default `true`)
  for GDPR-aware ingestion.
- `AssetMetadataMetrics` + `AssetMetadataActivitySource` for observability.

## Usage

```csharp
builder.AddGranitDocuments();
builder.AddGranitDocumentsAssetMetadata();

// Wire one or more extractor packages on top:
builder.AddGranitDocumentsAssetMetadataImaging();   // image/* EXIF / IPTC / XMP
builder.AddGranitDocumentsAssetMetadataPdf();        // PDF info dict + XMP
builder.AddGranitDocumentsAssetMetadataOffice();     // OOXML core properties
builder.AddGranitDocumentsAssetMetadataMedia();      // audio + video container

// Storage (provider-aware: jsonb on Postgres, nvarchar(max) elsewhere):
builder.AddGranitDocumentsAssetMetadataEntityFrameworkCore(o => o.UseNpgsql(connString));

// HTTP read surface:
app.MapGroup("/api")
   .MapGranitDocuments()
   .MapGranitDocumentsAssetMetadata();
```

## GDPR — GPS scrub on upload

`StripGpsOnUpload` defaults to `true`. When enabled, the image extractor's
upload-time hook rewrites the original blob without the GPS tags (and camera
serial) before the new `DocumentVersion.BlobDescriptorId` is finalised. Hosts
that legitimately need the GPS (real-estate, journalism, geo-tagging) opt out:

```csharp
builder.AddGranitDocumentsAssetMetadata(opts =>
{
    opts.StripGpsOnUpload = false;
});
```

The scrub is audited (`asset_metadata.gps_scrubbed.count` metric +
`AssetMetadata.GpsScrub` activity span).

## Observability

- Meter `Granit.Documents.AssetMetadata`:
  - `granit.documents.asset_metadata.extracted.count`
  - `granit.documents.asset_metadata.failed.count`
  - `granit.documents.asset_metadata.gps_scrubbed.count`
  - `granit.documents.asset_metadata.extraction.duration` (ms)
- `ActivitySource` `Granit.Documents.AssetMetadata` with spans
  `asset_metadata.pipeline.extract`, `asset_metadata.extractor.extract`,
  `asset_metadata.gps_scrub`.
- Tags: `tenant_id`, `source_content_type`, `extractor`, `error_type`.
