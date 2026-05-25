# Granit.TextExtraction

Pluggable byte-to-text extraction framework. Provides the core contracts
(`ITextExtractor`, `ITextExtractionPipeline`), the plain-text fallback extractor,
the defensive `LimitedStream` wrapper, and a unified options/diagnostics surface
for all concrete extractors (PDF, Office, HTML, OCR, Tika, ...).

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.TextExtraction
```

## Dependencies

- `Granit`

## Truncation contract

Every `ITextExtractor.ExtractAsync` accepts a `maxCharLength` parameter — extractors
MUST stop reading once produced text exceeds it. The returned `TextExtractionResult`
carries `IsTruncated`, `CharCount`, and `ExtractorName` so downstream consumers
(indexers, AI summarizers) can decide how to react.

Every extractor wraps its input in `LimitedStream` (capped by
`GranitTextExtractionOptions.MaxBodySizeBytes`) — this is the framework's defence
against decompression bombs and unbounded streams.

## Documentation

See the [full documentation](https://granit-fx.dev).
