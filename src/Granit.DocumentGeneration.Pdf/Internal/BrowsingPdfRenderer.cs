using System;
using System.Threading;
using System.Threading.Tasks;
using Granit.Browsing;
using Granit.Browsing.Capabilities;
using Granit.DocumentGeneration.Pdf.Options;
using Granit.DocumentGeneration.Pipeline;
using Granit.Templating.Keys;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using BrowsingNavigationOptions = Granit.Browsing.Options.NavigationOptions;
using BrowsingPaperFormat = Granit.Browsing.Capabilities.PaperFormat;
using BrowsingPdfOptions = Granit.Browsing.Capabilities.PdfOptions;

namespace Granit.DocumentGeneration.Pdf.Internal;

/// <summary>
/// <see cref="IDocumentRenderer"/> that converts HTML to PDF through
/// <c>Granit.Browsing</c>'s <see cref="IPdfCapability"/>. The active browser provider
/// is selected at host startup (<c>AddGranitBrowsingPuppeteerSharp()</c> /
/// <c>AddGranitBrowsingPlaywright()</c>); this renderer is engine-agnostic — it asks
/// for a page, hardens it (no JS, no network), sets the HTML, and renders.
/// </summary>
internal sealed partial class BrowsingPdfRenderer(
    IHeadlessBrowser browser,
    IPdfCapability pdfCapability,
    IOptions<PdfRenderOptions> options,
    ILogger<BrowsingPdfRenderer> logger) : IDocumentRenderer
{
    /// <inheritdoc/>
    public bool CanRender(DocumentFormat targetFormat) =>
        targetFormat == DocumentFormat.Pdf;

    /// <inheritdoc/>
    public async Task<DocumentResult> RenderAsync(
        string html,
        DocumentFormat targetFormat,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(html);

        PdfRenderOptions opts = options.Value;
        var renderTimeout = TimeSpan.FromMilliseconds(opts.RenderTimeoutMs);

        await using IBrowserPage page = await browser.AcquirePageAsync(
            new Granit.Browsing.Options.BrowserPageOptions
            {
                JavaScriptEnabled = false,    // CWE-94: rendering doesn't need JS
            },
            cancellationToken).ConfigureAwait(false);

        // CWE-918: SetContentAsync injects HTML via the page's CDP / driver — no
        // outbound request needed. Block any request the document might still emit
        // (referenced fonts, images served from foreign hosts, …) by aborting every
        // intercepted route.
        await page.RouteAsync("**/*", static (ctx, ct) => ctx.AbortAsync(cancellationToken: ct), cancellationToken).ConfigureAwait(false);

        await page.SetContentAsync(html, new BrowsingNavigationOptions
        {
            WaitUntil = LoadState.DomContentLoaded,
            Timeout = renderTimeout,
        }, cancellationToken).ConfigureAwait(false);

        BrowsingPdfOptions pdfOptions = new()
        {
            Format = ResolvePaperFormat(opts.PaperFormat),
            Landscape = opts.Landscape,
            PrintBackground = opts.PrintBackground,
            Margins = new PdfMargins(
                Top: opts.MarginTop,
                Right: opts.MarginRight,
                Bottom: opts.MarginBottom,
                Left: opts.MarginLeft),
            DisplayHeaderFooter = !string.IsNullOrEmpty(opts.HeaderTemplate) || !string.IsNullOrEmpty(opts.FooterTemplate),
            HeaderTemplate = string.IsNullOrEmpty(opts.HeaderTemplate) ? "<span></span>" : opts.HeaderTemplate,
            FooterTemplate = opts.FooterTemplate,
        };

        byte[] pdfBytes = await pdfCapability
            .RenderToPdfAsync(page, pdfOptions, cancellationToken)
            .WaitAsync(renderTimeout, cancellationToken)
            .ConfigureAwait(false);

        LogPdfRendered(pdfBytes.Length, opts.PaperFormat);
        return new DocumentResult(pdfBytes, DocumentFormat.Pdf);
    }

    /// <summary>
    /// Resolves a paper format string (<c>"A4"</c>, <c>"Letter"</c>, …) to a
    /// <see cref="BrowsingPaperFormat"/>. Falls back to <see cref="BrowsingPaperFormat.A4"/>
    /// for anything unrecognised — the abstraction set is intentionally smaller than
    /// what some engines expose, so legacy <c>"A0"</c> / <c>"A6"</c> / <c>"Ledger"</c>
    /// inputs collapse to the closest standard paper.
    /// </summary>
    internal static BrowsingPaperFormat ResolvePaperFormat(string format) =>
        format.ToUpperInvariant() switch
        {
            "A3" => BrowsingPaperFormat.A3,
            "A4" => BrowsingPaperFormat.A4,
            "A5" => BrowsingPaperFormat.A5,
            "LETTER" => BrowsingPaperFormat.Letter,
            "LEGAL" => BrowsingPaperFormat.Legal,
            "TABLOID" => BrowsingPaperFormat.Tabloid,
            _ => BrowsingPaperFormat.A4,
        };

    [LoggerMessage(Level = LogLevel.Debug, Message = "PDF rendered successfully ({Size} bytes, format={Format})")]
    private partial void LogPdfRendered(int size, string format);
}
