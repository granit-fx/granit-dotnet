using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Granit.Browsing.Capabilities;
using Granit.Browsing.Diagnostics;
using Granit.MultiTenancy;
using PuppeteerSharp.Media;
using BrowsingPaperFormat = Granit.Browsing.Capabilities.PaperFormat;
using BrowsingPdfOptions = Granit.Browsing.Capabilities.PdfOptions;
using PuppeteerPaperFormat = PuppeteerSharp.Media.PaperFormat;
using PuppeteerPdfOptions = PuppeteerSharp.PdfOptions;

namespace Granit.Browsing.PuppeteerSharp.Internal;

/// <summary>PuppeteerSharp implementation of <see cref="IPdfCapability"/>.</summary>
internal sealed class PuppeteerPdfCapability(BrowsingMetrics metrics, ICurrentTenant? currentTenant = null) : IPdfCapability
{
    private const string Engine = "chromium-puppeteer";

    private string? CurrentTenantId =>
        currentTenant is { IsAvailable: true } t ? t.Id?.ToString() : null;

    /// <inheritdoc/>
    public async Task<byte[]> RenderToPdfAsync(IBrowserPage page, BrowsingPdfOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(options);
        if (page is not PuppeteerBrowserPage puppeteerPage)
        {
            throw new InvalidOperationException(
                $"PuppeteerPdfCapability requires a page produced by {nameof(PuppeteerHeadlessBrowser)}; got {page.GetType().Name}.");
        }

        using Activity? activity = BrowsingActivitySource.Source.StartActivity(BrowsingActivitySource.PdfRender);
        var sw = Stopwatch.StartNew();

        PuppeteerPdfOptions puppeteerOpts = new()
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
            Scale = (decimal)options.Scale,
            PreferCSSPageSize = options.PreferCssPageSize,
        };

        if (options.Margins is { } m)
        {
            puppeteerOpts.MarginOptions = new MarginOptions
            {
                Top = m.Top,
                Right = m.Right,
                Bottom = m.Bottom,
                Left = m.Left,
            };
        }

        byte[] bytes = await puppeteerPage.UnderlyingPage
            .PdfDataAsync(puppeteerOpts)
            .ConfigureAwait(false);

        metrics.RecordRenderDuration(Engine, CurrentTenantId, "pdf", sw.Elapsed);
        return bytes;
    }

    private static PuppeteerPaperFormat MapFormat(BrowsingPaperFormat fmt) => fmt switch
    {
        BrowsingPaperFormat.A3 => PuppeteerPaperFormat.A3,
        BrowsingPaperFormat.A4 => PuppeteerPaperFormat.A4,
        BrowsingPaperFormat.A5 => PuppeteerPaperFormat.A5,
        BrowsingPaperFormat.Letter => PuppeteerPaperFormat.Letter,
        BrowsingPaperFormat.Legal => PuppeteerPaperFormat.Legal,
        BrowsingPaperFormat.Tabloid => PuppeteerPaperFormat.Tabloid,
        _ => PuppeteerPaperFormat.A4,
    };
}
