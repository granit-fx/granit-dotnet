# Granit.Browsing

Headless browser abstraction layer for Granit. Provides `IHeadlessBrowser`,
`IBrowserPage`, capability discovery (PDF generation, PDF viewer rendering,
accessibility tree, tracing, HAR recording), pool management, and sandbox
profiles. Concrete provider packages (`Granit.Browsing.PuppeteerSharp`,
`Granit.Browsing.Playwright`) plug into this surface.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Browsing
dotnet add package Granit.Browsing.PuppeteerSharp   # or .Playwright
```

## Usage

```csharp
// Host wires a provider. The base package alone is contracts-only.
services.AddGranitBrowsingPuppeteerSharp(opts =>
{
    opts.MaxBrowsers = 2;
    opts.MaxPagesPerBrowser = 8;
});

// A consumer that needs PDF generation injects both the browser and the
// capability. DI fails at boot if the active provider does not advertise
// BrowserCapabilities.PdfGeneration (e.g. Firefox / WebKit).
public sealed class HtmlToPdfRenderer(
    IHeadlessBrowser browser,
    IPdfCapability pdf)
{
    public async Task<byte[]> RenderAsync(string html, CancellationToken ct)
    {
        await using IBrowserPage page = await browser.AcquirePageAsync(ct: ct);
        await page.SetContentAsync(html, ct: ct);
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle, ct: ct);
        return await pdf.RenderToPdfAsync(page, new PdfOptions(), ct);
    }
}
```

## Capability matrix

| Capability | Chromium (PuppeteerSharp) | Chromium (Playwright) | Firefox (Playwright) | WebKit (Playwright) |
| --- | --- | --- | --- | --- |
| Screenshot | ✓ | ✓ | ✓ | ✓ |
| PDF generation | ✓ | ✓ | — | — |
| PDF viewer native | ✓ | ✓ | — | — |
| Network interception | ✓ (CDP full) | ✓ | partial | partial |
| Tracing | — | ✓ | ✓ | ✓ |
| HAR recording | partial | ✓ | ✓ | ✓ |
| Accessibility tree | ✓ | ✓ | ✓ | ✓ |
