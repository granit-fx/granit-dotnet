# Granit.Documents.AssetMetadata.Pdf

PDF metadata extractor for `Granit.Documents.AssetMetadata` (F17.6). Built on
top of the [`PdfPig`](https://github.com/UglyToad/PdfPig) NuGet (Apache-2.0).

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Documents.AssetMetadata.Pdf
```

## Usage

```csharp
builder.AddGranitDocumentsAssetMetadata();
builder.Services.AddGranitDocumentsAssetMetadataPdf();
```

`Granit.Documents.AssetMetadata.BackgroundJobs` picks the extractor up from
DI automatically — no further wiring.

## What it does

For every `application/pdf` source:

- Opens the PDF with PdfPig and reads the document `Information` dictionary
  plus the page count, dumping every entry verbatim into `RawMetadata` under
  the `pdf:` prefix (including custom keys not surfaced by the strongly-typed
  properties).
- Projects the well-known fields into the typed columns: `PageCount`, `Title`,
  `Author`, `Subject`, `Keywords`, `Producer`.

## Notes

The `PdfPig` library is permissive (Apache-2.0). Third-party notices in
[THIRD-PARTY-NOTICES.md](../../THIRD-PARTY-NOTICES.md).
