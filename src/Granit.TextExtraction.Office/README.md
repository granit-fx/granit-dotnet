# Granit.TextExtraction.Office

Word / Excel / PowerPoint text extractors for the
[`Granit.TextExtraction`](../Granit.TextExtraction/README.md) pipeline. Backed
by [`DocumentFormat.OpenXml`](https://github.com/dotnet/Open-XML-SDK) — pure
managed code, no native dependencies.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.TextExtraction.Office
```

## Dependencies

- `Granit.TextExtraction`
- `DocumentFormat.OpenXml`

## Extractors

| Extractor | Content type | Strategy |
| --------- | ------------ | -------- |
| `WordTextExtractor` | `application/vnd.openxmlformats-officedocument.wordprocessingml.document` | `MainDocumentPart.Document.Body.InnerText` (handles runs, paragraphs, tables). |
| `ExcelTextExtractor` | `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet` | Iterates each `WorksheetPart`, resolves shared-string cells via the `SharedStringTablePart`, joins by tab + newline. |
| `PowerPointTextExtractor` | `application/vnd.openxmlformats-officedocument.presentationml.presentation` | Iterates `SlidePart.Slide.InnerText` per slide, joined with blank lines. |

## Security posture

OpenXml files are ZIP archives. The extractors apply zip-bomb and
decompression-bomb defences **before** handing the stream to the parser:

- **Zip-entry cap** — count entries via `System.IO.Compression.ZipArchive`; abort
  if the count exceeds `GranitTextExtractionOptions.MaxZipEntries` (default
  10 000).
- **Decompression cap** — sum each entry's `Length`; abort if the total exceeds
  `GranitTextExtractionOptions.MaxDecompressedBytes` (default 500 MB).
- **Input cap** — `LimitedStream(MaxBodySizeBytes)` enforced upstream (default
  100 MB).
- **Per-part char cap** — `OpenSettings.MaxCharactersInPart` set to
  `MaxExtractedCharLength` so the parser refuses XML parts bigger than the
  output cap.

Documents that breach any cap are reported as an empty
`TextExtractionResult { IsTruncated: true }` — never thrown — so consumers
can flag the document as partial.

## Documentation

See the [full documentation](https://granit-fx.dev).
