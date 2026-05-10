using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Granit.Browsing.Capabilities;
using Granit.Browsing.Diagnostics;
using Granit.MultiTenancy;
using Microsoft.Playwright;
using BrowsingPaperFormat = Granit.Browsing.Capabilities.PaperFormat;
using BrowsingPdfOptions = Granit.Browsing.Capabilities.PdfOptions;

namespace Granit.Browsing.Playwright.Internal;

/// <summary>Microsoft.Playwright implementation of <see cref="IPdfCapability"/> (Chromium-only).</summary>
internal sealed class PlaywrightPdfCapability(BrowsingMetrics metrics, ICurrentTenant? currentTenant = null) : IPdfCapability
{
    private string? CurrentTenantId =>
        currentTenant is { IsAvailable: true } t ? t.Id?.ToString() : null;

    /// <inheritdoc/>
    public async Task<byte[]> RenderToPdfAsync(IBrowserPage page, BrowsingPdfOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(options);
        if (page is not PlaywrightBrowserPage playwrightPage)
        {
            throw new InvalidOperationException(
                $"PlaywrightPdfCapability requires a page produced by {nameof(PlaywrightHeadlessBrowser)}; got {page.GetType().Name}.");
        }
        if (!page.EngineName.StartsWith("chromium-", StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"PDF generation is Chromium-only. Active engine: {page.EngineName}.");
        }

        using Activity? activity = BrowsingActivitySource.Source.StartActivity(BrowsingActivitySource.PdfRender);
        var sw = Stopwatch.StartNew();

        var pwOpts = new PagePdfOptions
        {
            Format = options.Format is { } fmt ? MapFormat(fmt) : null,
            Width = options.Width,
            Height = options.Height,
            Landscape = options.Landscape,
            PrintBackground = options.PrintBackground,
            DisplayHeaderFooter = options.DisplayHeaderFooter,
            HeaderTemplate = options.HeaderTemplate,
            FooterTemplate = options.FooterTemplate,
            PageRanges = options.PageRanges,
            Scale = (float)options.Scale,
            PreferCSSPageSize = options.PreferCssPageSize,
        };
        if (options.Margins is { } m)
        {
            pwOpts.Margin = new Margin
            {
                Top = m.Top,
                Right = m.Right,
                Bottom = m.Bottom,
                Left = m.Left,
            };
        }

        byte[] bytes = await playwrightPage.UnderlyingPage.PdfAsync(pwOpts).ConfigureAwait(false);
        metrics.RecordRenderDuration(page.EngineName, CurrentTenantId, "pdf", sw.Elapsed);
        return bytes;
    }

    private static string MapFormat(BrowsingPaperFormat fmt) => fmt switch
    {
        BrowsingPaperFormat.A3 => "A3",
        BrowsingPaperFormat.A4 => "A4",
        BrowsingPaperFormat.A5 => "A5",
        BrowsingPaperFormat.Letter => "Letter",
        BrowsingPaperFormat.Legal => "Legal",
        BrowsingPaperFormat.Tabloid => "Tabloid",
        _ => "A4",
    };
}
