using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Granit.Browsing.Capabilities;
using Granit.Browsing.Diagnostics;
using Granit.MultiTenancy;
using BrowsingScreenshotFormat = Granit.Browsing.Options.ScreenshotFormat;
using BrowsingScreenshotOptions = Granit.Browsing.Options.ScreenshotOptions;

namespace Granit.Browsing.PuppeteerSharp.Internal;

/// <summary>
/// PuppeteerSharp implementation of <see cref="IPdfViewerCapability"/>. Loads the PDF in
/// Chromium's built-in PDF viewer and rasters individual pages by setting the viewport
/// to match the requested dimensions and screenshotting the viewer.
/// </summary>
internal sealed class PuppeteerPdfViewerCapability(BrowsingMetrics metrics, ICurrentTenant? currentTenant = null) : IPdfViewerCapability
{
    private const string Engine = "chromium-puppeteer";

    /// <inheritdoc/>
    public async Task<IPdfDocumentPage> OpenPdfAsync(IBrowserPage page, Stream pdf, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(pdf);
        if (page is not PuppeteerBrowserPage puppeteerPage)
        {
            throw new InvalidOperationException(
                $"PuppeteerPdfViewerCapability requires a page produced by {nameof(PuppeteerHeadlessBrowser)}; got {page.GetType().Name}.");
        }

        using Activity? activity = BrowsingActivitySource.Source.StartActivity(BrowsingActivitySource.PdfViewerOpen);

        // Buffer the bytes so the underlying viewer can request them via a data URL —
        // robust across providers and avoids holding the original stream open.
        using MemoryStream buffer = new();
        await pdf.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        byte[] bytes = buffer.ToArray();

        // The Chromium PDF viewer is loaded via a data URL. PageCount is derived from the
        // PDF trailer — we use a lightweight heuristic by counting "/Type /Page" markers.
        int pageCount = CountPdfPages(bytes);
        if (pageCount <= 0)
        {
            pageCount = 1;
        }

        string dataUrl = "data:application/pdf;base64," + System.Convert.ToBase64String(bytes);
        await puppeteerPage.NavigateAsync(dataUrl, options: null, cancellationToken).ConfigureAwait(false);
        await puppeteerPage.WaitForLoadStateAsync(LoadState.Load, timeout: System.TimeSpan.FromSeconds(15),
            cancellationToken).ConfigureAwait(false);

        return new PuppeteerPdfDocumentPage(puppeteerPage, pageCount, metrics, currentTenant);
    }

    private static int CountPdfPages(byte[] bytes)
    {
        // Conservative scan over the raw PDF — sufficient for the ~80% case and avoids
        // bundling a PDF parser in the contract layer. Providers wanting an exact count
        // can override the impl.
        string text = System.Text.Encoding.Latin1.GetString(bytes);
        int idx = 0;
        int count = 0;
        const string marker = "/Type /Page";
        while ((idx = text.IndexOf(marker, idx, System.StringComparison.Ordinal)) >= 0)
        {
            // Skip "/Type /Pages" containers (the catalog, not a leaf page).
            int after = idx + marker.Length;
            if (after < text.Length && text[after] == 's')
            {
                idx = after;
                continue;
            }
            count++;
            idx = after;
        }
        return count;
    }
}

internal sealed class PuppeteerPdfDocumentPage(
    PuppeteerBrowserPage page,
    int pageCount,
    BrowsingMetrics metrics,
    ICurrentTenant? currentTenant) : IPdfDocumentPage
{
    private const string Engine = "chromium-puppeteer";

    private string? CurrentTenantId =>
        currentTenant is { IsAvailable: true } t ? t.Id?.ToString() : null;

    /// <inheritdoc/>
    public int PageCount { get; } = pageCount;

    /// <inheritdoc/>
    public async Task<byte[]> RenderPageToImageAsync(int pageIndex, RenditionDimensions dimensions, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(pageIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(pageIndex, PageCount);
        ArgumentNullException.ThrowIfNull(dimensions);

        using Activity? activity = BrowsingActivitySource.Source.StartActivity(BrowsingActivitySource.PdfViewerRenderPage);
        var sw = Stopwatch.StartNew();

        // Drive the viewer to the requested page and resize the viewport to the target
        // dimensions; the viewer respects the viewport for its internal rendering.
        await page.UnderlyingPage.SetViewportAsync(new global::PuppeteerSharp.ViewPortOptions
        {
            Width = dimensions.Width,
            Height = dimensions.Height,
        }).ConfigureAwait(false);

        await page.EvaluateAsync<bool>(
            $"(async () => {{ try {{ window.location.hash = '#page={pageIndex + 1}'; return true; }} catch {{ return false; }} }})()",
            cancellationToken).ConfigureAwait(false);

        // Settle paint before capturing.
        await Task.Delay(System.TimeSpan.FromMilliseconds(150), cancellationToken).ConfigureAwait(false);

        byte[] png = await page.ScreenshotAsync(new BrowsingScreenshotOptions
        {
            Format = BrowsingScreenshotFormat.Png,
        }, cancellationToken).ConfigureAwait(false);

        metrics.RecordRenderDuration(Engine, CurrentTenantId, "pdf_viewer_render_page", sw.Elapsed);
        return png;
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
