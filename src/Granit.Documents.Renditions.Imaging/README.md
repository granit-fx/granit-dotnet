# Granit.Documents.Renditions.Imaging

`image/* → image/*` rendition provider for `Granit.Documents.Renditions` (F16.5).
Resizes + re-encodes uploaded images via `Granit.Imaging`, strips EXIF / IPTC /
XMP metadata for GDPR compliance.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Documents.Renditions.Imaging
```

Pick an imaging backend (currently `Granit.Imaging.MagickNet`):

```bash
dotnet add package Granit.Imaging.MagickNet
```

## What it does

- Registers `ImagingRenditionProvider` (`image/* → image/*`) with the pipeline.
- Subscribes to `DocumentVersionAddedEvent` with a **synchronous thumbnail hook**:
  for image uploads, generates a thumbnail in-band and persists a `Ready`
  rendition row before the F16.4 background handler runs. The async handler
  finds the existing Ready row and short-circuits.
- The inline path respects `GranitRenditionsOptions.InlineThumbnailMaxBytes`
  (default 100 KB): larger outputs are dropped and left to the background
  handler so the upload path stays fast.

## Usage

```csharp
builder.AddGranitDocuments();
builder.AddGranitDocumentsRenditions();
builder.AddGranitDocumentsRenditionsEntityFrameworkCore(opts => opts.UseNpgsql(conn));
builder.AddGranitDocumentsRenditionsWolverine();
builder.AddGranitImagingMagickNet();
builder.AddGranitDocumentsRenditionsImaging();
```

## Configuration

| Option | Default | Effect |
| --- | --- | --- |
| `Documents:Renditions:Thumbnail:Width` | 200 | Inline thumbnail width (px). |
| `Documents:Renditions:Thumbnail:Height` | 200 | Inline thumbnail height (px). |
| `Documents:Renditions:Thumbnail:Format` | `image/webp` | Output MIME for the inline thumbnail. |
| `Documents:Renditions:Thumbnail:Quality` | 75 | Lossy quality knob (0–100). |
| `Documents:Renditions:InlineThumbnailMaxBytes` | `102400` (100 KB) | Drop the inline thumbnail above this size. |
