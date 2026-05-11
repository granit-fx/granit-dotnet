# Granit.Documents.Renditions.Pdf

`application/pdf → image/png` rendition provider for `Granit.Documents.Renditions`
(F16.6). Renders the requested PDF page through
`Granit.Browsing.IPdfViewerCapability`; chains with the Imaging provider for
JPEG / WebP / AVIF targets.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Documents.Renditions.Pdf
```

Hosts also need a Chromium-backed `IHeadlessBrowser` — pick one:

```bash
dotnet add package Granit.Browsing.Playwright
# or
dotnet add package Granit.Browsing.PuppeteerSharp
```

## What it does

- Registers `PdfRenditionProvider` (`application/pdf → image/png`) with the
  rendition pipeline.
- Output is always PNG. Targets like `image/webp` are reached automatically by
  chaining with `Granit.Documents.Renditions.Imaging` (`pdf → png → webp`, two
  hops, within `GranitRenditionsOptions.MaxChainLength`).
- Fails fast at boot when no `IHeadlessBrowser` is registered, when the engine
  does not advertise `BrowserCapabilities.PdfViewerNative`, or when
  `IPdfViewerCapability` is missing from DI — so missing wiring surfaces during
  startup instead of on the first PDF upload.

## Usage

```csharp
builder.AddGranitDocuments();
builder.AddGranitDocumentsRenditions();
builder.AddGranitDocumentsRenditionsEntityFrameworkCore(opts => opts.UseNpgsql(conn));
builder.AddGranitDocumentsRenditionsBackgroundJobs();
builder.AddGranitImagingMagickNet();
builder.AddGranitDocumentsRenditionsImaging();
builder.AddGranitBrowsingPlaywright(o => o.Engine = BrowserEngine.Chromium);
builder.AddGranitDocumentsRenditionsPdf();
```
