# Granit.Documents.Renditions.BackgroundJobs

Event-driven background generation for `Granit.Documents.Renditions` (F16.4).
Subscribes to `DocumentVersionAddedEvent` and runs the rendition pipeline
asynchronously per registered target.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Documents.Renditions.BackgroundJobs
```

## What it does

1. Receives `DocumentVersionAddedEvent` over the local bus.
2. Resolves the rendition set to generate via `IRenditionTypePolicy`
   (`DefaultRenditionTypePolicy` ships with sensible defaults — see below).
3. For each target: creates a `Pending` row, runs the rendition pipeline,
   uploads the bytes via `IBlobStorage`, and updates the row to `Ready`
   (or `Failed` with the reason on a terminal error).
4. Bumps `TenantStorageQuota.RenditionUsageBytes` on success.
5. Caps simultaneous generations at `GranitRenditionsOptions.MaxConcurrentGenerations`.

## Default rendition policy

| Source MIME family | Generated renditions |
| --- | --- |
| `image/*` | `Thumbnail` (configured format) + `Web` (`image/webp`) |
| `application/pdf` | `Thumbnail` |
| Office MIMEs (docx / xlsx / pptx + legacy doc / xls / ppt) | `Thumbnail` |
| anything else | (none — handler short-circuits) |

Hosts override by registering their own `IRenditionTypePolicy` before
`AddGranitDocumentsRenditionsBackgroundJobs()`.

## Usage

```csharp
builder.AddGranitDocuments();
builder.AddGranitDocumentsRenditions();
builder.AddGranitDocumentsRenditionsEntityFrameworkCore(opts => opts.UseNpgsql(connString));
builder.AddGranitDocumentsRenditionsBackgroundJobs();
```

Providers (`Granit.Documents.Renditions.Imaging`, `.Pdf`, `.Office`) plug in
independently — without at least one matching provider in the container the
pipeline will throw `RenditionPipelineException` and every row lands as `Failed`.
