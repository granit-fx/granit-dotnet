# Granit.Documents.AssetMetadata.Imaging

Image EXIF / IPTC / XMP extractor for `Granit.Documents.AssetMetadata` (F17.5).
Built on top of the [`MetadataExtractor`](https://github.com/drewnoakes/metadata-extractor-dotnet)
NuGet (Apache-2.0).

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Documents.AssetMetadata.Imaging
```

## Usage

```csharp
builder.AddGranitDocumentsAssetMetadata();
builder.Services.AddGranitDocumentsAssetMetadataImaging();
```

`Granit.Documents.AssetMetadata.BackgroundJobs` picks the extractor up from
DI automatically — no further wiring.

## What it does

For every `image/*` source:

- Reads every metadata directory MetadataExtractor exposes (EXIF IFD0, EXIF
  SubIFD, GPS, IPTC, XMP, JPEG, PNG, WebP …) and dumps tags verbatim into
  `RawMetadata` under prefixes `exif:`, `iptc:`, `xmp:`, `jpeg:`, `png:`, `webp:`.
- Projects the well-known fields into the typed columns: dimensions, camera
  make / model / lens, ISO, F-number, exposure time, original capture date,
  GPS latitude / longitude / altitude.
- When `GranitAssetMetadataOptions.StripGpsOnUpload` is enabled (default), GPS
  values are removed from both the typed projection and the raw archive. The
  original blob is scrubbed in a separate flow (F17.9) so the GPS values never
  hit cold storage in the first place.

## Notes

The `MetadataExtractor` library is permissive (Apache-2.0). Third-party notices
in [THIRD-PARTY-NOTICES.md](../../THIRD-PARTY-NOTICES.md).
