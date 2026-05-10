# Granit.DocumentGeneration.Pdf

PDF document generation for Granit. Renders HTML to PDF through
`Granit.Browsing`'s `IPdfCapability` — bring your own browser provider
(`Granit.Browsing.PuppeteerSharp` or `Granit.Browsing.Playwright` with the
Chromium engine).

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.DocumentGeneration.Pdf
dotnet add package Granit.Browsing.PuppeteerSharp   # or .Playwright
```

## Usage

```csharp
// Register the browser provider FIRST — AddGranitDocumentGenerationPdf()
// fails fast at startup if no IHeadlessBrowser is registered.
services.AddGranitBrowsingPuppeteerSharp();
services.AddGranitDocumentGenerationPdf();
```

Configuration section `DocumentGeneration:Pdf` binds to `PdfRenderOptions`
(paper format, orientation, margins, header/footer templates,
`PrintBackground`, `RenderTimeoutMs`). Browser-pool sizing, Chromium executable
path, and sandbox flags live on the `Granit.Browsing` provider's own
configuration sections.

## Dependencies

- `Granit.DocumentGeneration`
- `Granit.Browsing` (with a registered provider that advertises
  `BrowserCapabilities.PdfGeneration`)

## Documentation

See the [full documentation](https://granit-fx.dev).
