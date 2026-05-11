# Granit.Documents.AssetMetadata.BackgroundJobs

Event-driven background extraction for `Granit.Documents.AssetMetadata` (F17.4).
Subscribes to `DocumentVersionAddedEvent` and runs the registered
`IAssetMetadataExtractor` chain asynchronously.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Documents.AssetMetadata.BackgroundJobs
```

## What it does

1. Receives `DocumentVersionAddedEvent` over the local bus.
2. Looks up or creates a `Pending` `DocumentAssetMetadata` row for the
   version (idempotent — re-runs on a `Ready` row are a no-op).
3. Marks the row `Extracting`, fetches the source bytes through a presigned
   download URL, and runs every registered extractor.
4. Merges the per-extractor results, then marks the row `Ready` and emits
   `AssetMetadataExtractedEvent` — or `Failed` (with reason) on pipeline error.
5. Caps simultaneous extractions at
   `GranitAssetMetadataOptions.MaxConcurrentExtractions` (default 4).

## Empty extractor chain

With no `IAssetMetadataExtractor` registered, the pipeline produces an empty
result set: the row lands `Ready` with `ExtractorCount = 0`. Hosts add
extractors per provider — image (F17.5), PDF (F17.6), Office (F17.7),
audio / video (F17.8).

## Usage

```csharp
builder.AddGranitDocuments();
builder.AddGranitDocumentsAssetMetadata();
builder.AddGranitDocumentsAssetMetadataEntityFrameworkCore(opts => opts.UseNpgsql(connString));

// add extractors before / after — order doesn't matter (first-write wins on typed columns)
builder.AddGranitDocumentsAssetMetadataImaging();

builder.Services.AddGranitDocumentsAssetMetadataBackgroundJobs();
```

The Wolverine handler `DocumentVersionAddedAssetMetadataHandler` is discovered
automatically from the assembly.
