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

> **Module wiring.** Wire this provider via
> `[DependsOn(typeof(GranitBrowsingPuppeteerSharpModule))]` on your host module,
> which pulls in `GranitBrowsingModule` and registers the required services
> (`BrowsingMetrics`, sandbox profile, HAR scrubber) the capabilities depend on.
> The `AddGranitBrowsingPuppeteerSharp(...)` call below is the in-module
> configuration entry point, not a standalone bootstrap — calling it on a bare
> `IServiceCollection` without the core services registered leaves dependencies
> unresolved and fails at DI resolution time.

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
