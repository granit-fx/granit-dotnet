# Granit.TextExtraction.Text

HTML and Markdown text extractors for the
[`Granit.TextExtraction`](../Granit.TextExtraction/README.md) pipeline. Consumed
by `Granit.Documents.Search` and any module ingesting text from documents,
emails, or knowledge bases.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.TextExtraction.Text
```

## Dependencies

- `Granit.TextExtraction`
- `Granit.Html` + `Granit.Html.AngleSharp` (HTML→plain-text via AngleSharp)
- `Markdig` (Markdown→plain-text)

## Extractors

| Extractor | MIME types | Notes |
| --------- | ---------- | ----- |
| `HtmlTextExtractor` | `text/html`, `application/xhtml+xml` | Consumes the keyed `HtmlConverterKeys.Untrusted` `IHtmlToPlainTextConverter` registered by `Granit.Html.AngleSharp`. SSRF-safe by design — the untrusted profile never resolves external resources. |
| `MarkdownTextExtractor` | `text/markdown`, `text/x-markdown` | Uses `Markdig.Markdown.ToPlainText` with the `UseAdvancedExtensions()` pipeline (tables, footnotes, task lists, …). |

## Documentation

See the [full documentation](https://granit-fx.dev).
