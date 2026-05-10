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

namespace Granit.Browsing.Playwright.Internal;

/// <summary>
/// Microsoft.Playwright implementation of <see cref="IPdfViewerCapability"/>
/// (Chromium-only). Uses the same data-URL strategy as the PuppeteerSharp provider.
/// </summary>
internal sealed class PlaywrightPdfViewerCapability(BrowsingMetrics metrics, ICurrentTenant? currentTenant = null) : IPdfViewerCapability
{
    /// <inheritdoc/>
    public async Task<IPdfDocumentPage> OpenPdfAsync(IBrowserPage page, Stream pdf, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(pdf);
        if (page is not PlaywrightBrowserPage playwrightPage)
        {
            throw new InvalidOperationException(
                $"PlaywrightPdfViewerCapability requires a page produced by {nameof(PlaywrightHeadlessBrowser)}; got {page.GetType().Name}.");
        }
        if (!page.EngineName.StartsWith("chromium-", StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"Native PDF viewer is Chromium-only. Active engine: {page.EngineName}.");
        }

        using Activity? activity = BrowsingActivitySource.Source.StartActivity(BrowsingActivitySource.PdfViewerOpen);

        using MemoryStream buffer = new();
        await pdf.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        byte[] bytes = buffer.ToArray();

        int pageCount = CountPdfPages(bytes);
        if (pageCount <= 0)
        {
            pageCount = 1;
        }

        string dataUrl = "data:application/pdf;base64," + Convert.ToBase64String(bytes);
        await playwrightPage.NavigateAsync(dataUrl, options: null, cancellationToken).ConfigureAwait(false);
        await playwrightPage.WaitForLoadStateAsync(LoadState.Load, timeout: TimeSpan.FromSeconds(15),
            cancellationToken).ConfigureAwait(false);

        return new PlaywrightPdfDocumentPage(playwrightPage, pageCount, metrics, currentTenant);
    }

    private static int CountPdfPages(byte[] bytes)
    {
        string text = System.Text.Encoding.Latin1.GetString(bytes);
        int idx = 0;
        int count = 0;
        const string marker = "/Type /Page";
        while ((idx = text.IndexOf(marker, idx, StringComparison.Ordinal)) >= 0)
        {
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

internal sealed class PlaywrightPdfDocumentPage(
    PlaywrightBrowserPage page,
    int pageCount,
    BrowsingMetrics metrics,
    ICurrentTenant? currentTenant) : IPdfDocumentPage
{
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

        await page.UnderlyingPage.SetViewportSizeAsync(dimensions.Width, dimensions.Height).ConfigureAwait(false);
        await page.EvaluateAsync<bool>(
            $"(async () => {{ try {{ window.location.hash = '#page={pageIndex + 1}'; return true; }} catch {{ return false; }} }})()",
            cancellationToken).ConfigureAwait(false);
        await Task.Delay(TimeSpan.FromMilliseconds(150), cancellationToken).ConfigureAwait(false);

        byte[] png = await page.ScreenshotAsync(new BrowsingScreenshotOptions
        {
            Format = BrowsingScreenshotFormat.Png,
        }, cancellationToken).ConfigureAwait(false);

        metrics.RecordRenderDuration(page.EngineName, CurrentTenantId, "pdf_viewer_render_page", sw.Elapsed);
        return png;
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
