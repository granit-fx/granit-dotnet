# Granit.Browsing.Playwright

Microsoft.Playwright implementation of `Granit.Browsing`. Supports Chromium,
Firefox, and WebKit. Adds tracing and HAR recording capabilities on top of the
PuppeteerSharp baseline.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Browsing.Playwright
```

## Usage

```csharp
services.AddGranitBrowsingPlaywright(
    configureBrowsing: opts => { opts.MaxBrowsers = 2; },
    configurePlaywright: opts =>
    {
        opts.Engine = BrowserEngine.Chromium;   // Firefox / Webkit also supported
    });
```

The first-run browser install is handled by an `IHostedService` that runs
`playwright install <engine>` at boot. Bundle browsers in your container image
and set `PlaywrightOptions.SkipBrowserInstall = true` to opt out.

## Capability matrix

| Capability | Chromium | Firefox | WebKit |
| --- | --- | --- | --- |
| Screenshot | ✓ | ✓ | ✓ |
| PDF generation | ✓ | — | — |
| PDF viewer native | ✓ | — | — |
| Network interception | ✓ | partial | partial |
| Accessibility tree | ✓ | ✓ | ✓ |
| Tracing | ✓ | ✓ | ✓ |
| HAR recording | ✓ | ✓ | ✓ |

`IPdfCapability` and `IPdfViewerCapability` are registered conditionally — the
DI factory throws at resolution time when the configured engine is Firefox or
WebKit, with a clear message pointing the host at Chromium or the
`Granit.Browsing.PuppeteerSharp` package.
