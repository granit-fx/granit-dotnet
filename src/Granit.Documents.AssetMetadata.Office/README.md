# Granit.Documents.AssetMetadata.Office

Office (OOXML) metadata extractor for `Granit.Documents.AssetMetadata` (F17.7).
Built on top of the [`DocumentFormat.OpenXml`](https://github.com/dotnet/Open-XML-SDK)
NuGet (MIT).

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Documents.AssetMetadata.Office
```

## Usage

```csharp
builder.AddGranitDocumentsAssetMetadata();
builder.Services.AddGranitDocumentsAssetMetadataOffice();
```

`Granit.Documents.AssetMetadata.BackgroundJobs` picks the extractor up from
DI automatically — no further wiring.

## What it does

For every OOXML source — Word (`.docx`), Excel (`.xlsx`), PowerPoint
(`.pptx`):

- Opens the document read-only with `DocumentFormat.OpenXml` and reads the
  `core.xml` package properties plus the `app.xml` extended properties.
- Dumps every property verbatim into `RawMetadata` under the `office:`
  prefix (including custom application-specific keys).
- Projects the well-known fields into the typed columns: `Title`, `Author`,
  `Subject`, `Keywords`, `Revision`, `LastModifiedBy`, `Producer` (from
  `Application`) plus `PageCount` (Word `Pages`, PowerPoint `Slides`).
  Excel has no concept of pages — `PageCount` stays `null`.

Legacy binary formats (`.doc` / `.xls` / `.ppt`) are out of scope:
DocumentFormat.OpenXml does not read them and the available legacy parsers
are LGPL.

## Notes

The `DocumentFormat.OpenXml` library is permissive (MIT). Third-party
notices in [THIRD-PARTY-NOTICES.md](../../THIRD-PARTY-NOTICES.md).
