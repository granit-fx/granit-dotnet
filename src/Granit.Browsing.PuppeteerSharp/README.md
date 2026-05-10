# Granit.Browsing.PuppeteerSharp

PuppeteerSharp implementation of `Granit.Browsing` — Chromium-backed
`IHeadlessBrowser` with full capability support for screenshots, PDF generation,
PDF viewer rendering, network interception, JavaScript injection, device
emulation, media emulation, and accessibility tree snapshots.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Browsing.PuppeteerSharp
```

## Usage

```csharp
services.AddGranitBrowsingPuppeteerSharp(
    configureBrowsing: opts => { opts.MaxBrowsers = 2; opts.MaxPagesPerBrowser = 8; },
    configurePuppeteer: opts => { opts.DisableSandbox = false; });
```

The first-run Chromium download is handled by an `IHostedService` that runs at
boot. In containerised production, bundle Chromium in the image and set
`PuppeteerSharpOptions.ChromiumExecutablePath` + `SkipChromiumDownload = true`.

## Capabilities advertised

- `Screenshot`
- `PdfGeneration`
- `PdfViewerNative`
- `NetworkInterception`
- `JavaScriptInjection`
- `EmulateDevice`
- `EmulateMedia`
- `AccessibilityTree`

Tracing and HAR recording are not advertised (Playwright provider only).
