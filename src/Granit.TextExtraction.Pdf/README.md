# Granit.TextExtraction.Pdf

PDF text extractor for the
[`Granit.TextExtraction`](../Granit.TextExtraction/README.md) pipeline. Backed
by [PdfPig](https://github.com/UglyToad/PdfPig) — pure managed code, no native
dependencies. Consumed by `Granit.Documents.Search` and any module ingesting
text from uploaded documents.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.TextExtraction.Pdf
```

## Dependencies

- `Granit.TextExtraction`
- `PdfPig`

## Extraction strategy

- **Primary**: `ContentOrderTextExtractor.GetText(page)` per page, joined with
  blank lines. Produces reading-order text suitable for indexing.
- **Fallback**: raw `Page.GetWords()` joined with spaces when the order
  extractor throws on malformed page content. Keeps degraded PDFs indexable
  instead of failing the whole document.

## Security posture

- Input wrapped in `LimitedStream` (`MaxBodySizeBytes` cap, VULN-001).
- **Password-protected PDFs** (`PdfDocumentEncryptedException`) are reported
  as an empty `TextExtractionResult` with `IsTruncated = true` and the
  `granit.text_extraction.document.skipped` metric incremented. No silent
  password prompt is attempted.
- **Malformed PDFs** (`PdfDocumentFormatException`) follow the same path.

## Documentation

See the [full documentation](https://granit-fx.dev).
