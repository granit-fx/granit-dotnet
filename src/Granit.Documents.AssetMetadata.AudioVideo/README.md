# Granit.Documents.AssetMetadata.AudioVideo

Audio / video metadata extractor for `Granit.Documents.AssetMetadata`
(F17.8). Built on top of the [`TagLibSharp`](https://github.com/mono/taglib-sharp)
NuGet (LGPL-2.1).

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Documents.AssetMetadata.AudioVideo
```

## Usage

```csharp
builder.AddGranitDocumentsAssetMetadata();
builder.Services.AddGranitDocumentsAssetMetadataAudioVideo();
```

`Granit.Documents.AssetMetadata.BackgroundJobs` picks the extractor up
from DI automatically — no further wiring.

## What it does

For every `audio/*` and `video/*` source:

- Wraps the input stream in a `TagLib.File.IFileAbstraction` (buffering
  non-seekable streams to a `MemoryStream` first) and hands it to
  TagLibSharp. The container is inferred from the MIME-mapped extension
  (`.mp3`, `.flac`, `.ogg`, `.wav`, `.aac`, `.m4a`, `.mp4`, `.webm`,
  `.mov`, `.mkv`, ...). Unknown subtypes fall back to `.bin`.
- Dumps every recognised tag and codec field verbatim into `RawMetadata`
  under the `audio:` or `video:` prefix (picked based on the source MIME).
- Projects the well-known fields into the typed columns: `Title`,
  `Artist`, `Album`, `Genre`, `TrackNumber`, `TakenAt` (from the
  recording year), `DurationMs`, `Codec`, `Bitrate`, plus `Width` and
  `Height` for video sources.

If TagLibSharp cannot identify the container
(`TagLib.UnsupportedFormatException`) the exception bubbles up — the
pipeline wraps it in `AssetMetadataExtractionException` so the row is
marked Failed with the offending extractor name.

## Notes

`TagLibSharp` is **LGPL-2.1**. Granit links to the NuGet assembly
dynamically (it is shipped as its own DLL); copyleft does **not**
propagate to your application code or to the Apache-2.0 framework, but
downstream hosts inherit the obligation to keep the TagLibSharp assembly
distributable and replaceable. Third-party notices in
[THIRD-PARTY-NOTICES.md](../../THIRD-PARTY-NOTICES.md).
