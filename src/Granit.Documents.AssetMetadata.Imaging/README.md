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

## F17.9 — Synchronous GPS scrub on upload

The package also ships a `StripGpsHandler` that subscribes to
`DocumentVersionAddedEvent` and runs synchronously alongside the F17.4
background extractor. The handler:

1. Downloads the original blob through `IBlobStorage.CreateDownloadUrlAsync`.
2. Locates the GPS-IFD pointer entry (TIFF tag `0x8825`) inside the EXIF profile's
   raw bytes and rewrites the entry to a benign / unknown tag id — every other
   IFD entry stays at the same byte offset, so the EXIF profile retains every
   non-GPS field (`Make`, `Model`, `DateTimeOriginal`, ISO, …) and the embedded
   ICC colour profile is untouched.
3. Uploads the scrubbed bytes through `IBlobStorage.InitiateUploadAsync` +
   presigned PUT + `ConfirmUploadAsync`.
4. Calls `IDocumentService.ReplaceVersionBlobAsync` to atomically swap the
   `DocumentVersion.BlobDescriptorId`, rebalance the tenant quota
   (decrement old, increment new) and emit `DocumentBlobScrubbedEvent` for
   the GDPR / ISO 27001 A.12.4.1 audit trail.
5. Soft-deletes the original blob (BlobStorage retains the descriptor row for
   the 3-year audit retention; only the bytes are erased).

Ordering vs F17.4 is pinned by an explicit re-fetch inside the F17.4
`AssetMetadataGenerationService` — the extractor always reads the
*current* `DocumentVersion.BlobDescriptorId`, never the snapshot from the
original event. The two handlers therefore converge on the scrubbed blob
regardless of local-vs-Wolverine queue ordering.

Toggle off `GranitAssetMetadataOptions.StripGpsOnUpload` (default `true`)
to keep originals verbatim — both the scrub handler *and* the projection-
side GPS drop short-circuit.

**Future hardening.** The current scrub rewrites the GPS-IFD pointer entry in
place — the orphaned GPS sub-IFD bytes remain in the EXIF blob but are
unreachable through the IFD chain. A future iteration will compact the EXIF
profile (drop the orphaned bytes) and extend the scrub to camera serial
numbers (`ExifTag.SerialNumber` and the `MakerNotes` sub-IFD, which can also
leak per-device identifiers).

## Notes

The `MetadataExtractor` library is permissive (Apache-2.0). Third-party notices
in [THIRD-PARTY-NOTICES.md](../../THIRD-PARTY-NOTICES.md).
