using System.Diagnostics;
using System.Text;
using Granit.Browsing;
using Granit.Browsing.Capabilities;
using Granit.Browsing.Pages;
using Granit.DocumentGeneration.Pdf.Diagnostics;
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

        // Bound HTML size to protect the Chromium renderer process from OOM.
        // Char count overestimates byte count when UTF-16 chars are ASCII (good) and underestimates
        // for surrogate pairs (rare). Use ByteCount for a tight check.
        int htmlByteLength = Encoding.UTF8.GetByteCount(html);
        if (htmlByteLength > opts.MaxHtmlBytes)
        {
            throw new ArgumentException(
                $"HTML payload of {htmlByteLength} bytes exceeds MaxHtmlBytes={opts.MaxHtmlBytes}.",
                nameof(html));
        }

        // Header/footer templates are trusted strings from configuration. Validate that
        // no remote-fetch primitive smuggled in; defense-in-depth against config-store compromise
        // / tenant-controlled overrides (the page-level route-abort does not apply to Chromium's
        // print-preview header/footer render context).
        ValidateTrustedTemplate(opts.HeaderTemplate, nameof(opts.HeaderTemplate));
        ValidateTrustedTemplate(opts.FooterTemplate, nameof(opts.FooterTemplate));

        var renderTimeout = TimeSpan.FromMilliseconds(opts.RenderTimeoutMs);

        using Activity? activity = PdfRenderingActivitySource.Source.StartActivity(PdfRenderingActivitySource.RenderPdf);
        activity?.SetTag("pdf.format", opts.PaperFormat);
        activity?.SetTag("pdf.html_bytes", htmlByteLength);

        await using IBrowserPage page = await browser.AcquirePageAsync(
            new Granit.Browsing.Options.BrowserPageOptions
            {
                JavaScriptEnabled = false,    // CWE-94: rendering doesn't need JS
            },
            cancellationToken).ConfigureAwait(false);

        // CWE-918: SetContentAsync injects HTML via the page's CDP / driver — no
        // outbound request needed. Block any request the document might still emit
        // (referenced fonts, images served from foreign hosts, …) by aborting every
        // intercepted route. Note: route() does not intercept data:/blob:/about: schemes
        // (no network) — combined with JavaScriptEnabled=false above, this leaves no
        // user-controlled exfil primitive in the main page render.
        await page.RouteAsync(
            RoutePattern.Parse("**/*"),
            static (_, _) => ValueTask.FromResult(RouteDecision.Abort("blocked")),
            cancellationToken).ConfigureAwait(false);

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

        try
        {
            byte[] pdfBytes = await pdfCapability
                .RenderToPdfAsync(page, pdfOptions, cancellationToken)
                .WaitAsync(renderTimeout, cancellationToken)
                .ConfigureAwait(false);

            activity?.SetTag("pdf.bytes", pdfBytes.Length);
            activity?.SetStatus(ActivityStatusCode.Ok);
            LogPdfRendered(pdfBytes.Length, opts.PaperFormat);
            return new DocumentResult(pdfBytes, DocumentFormat.Pdf);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Rejects header/footer templates that embed remote-fetch primitives. The templates are
    /// trusted strings from configuration — this is defense-in-depth (config-store compromise,
    /// tenant overrides). Chromium renders these in a separate print-preview context that
    /// bypasses the main page's route filter.
    /// </summary>
    private static void ValidateTrustedTemplate(string? template, string fieldName)
    {
        if (string.IsNullOrEmpty(template))
        {
            return;
        }

        // Case-insensitive contains. We accept HTML structure freely; what we forbid is anything
        // that would trigger an outbound fetch (img, script, link, iframe, object, source, video,
        // audio, embed, srcset, CSS url(...), or a base href that re-targets the document).
        ReadOnlySpan<string> forbidden =
        [
            "http://", "https://", "//",
            "<script", "<iframe", "<object", "<embed",
            "<img", "<link", "<source", "<video", "<audio", "<base",
            "srcset", "url(",
        ];

        foreach (string needle in forbidden)
        {
            if (template.Contains(needle, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"{fieldName} contains '{needle}' which can trigger an outbound fetch. " +
                    "PDF header/footer templates must not reference remote resources " +
                    "(CSS-only content is supported — see XML/CSS-only templating).");
            }
        }
    }

    /// <summary>
    /// Resolves a paper format string (<c>"A4"</c>, <c>"Letter"</c>, …) to a
    /// <see cref="BrowsingPaperFormat"/>. Falls back to <see cref="BrowsingPaperFormat.A4"/>
    /// for anything unrecognised — <see cref="PdfRenderOptions.PaperFormat"/> is validated at
    /// boot via <c>[AllowedValues]</c>, so this fallback only matters in tests / direct calls.
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
