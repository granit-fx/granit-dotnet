using Granit.DocumentGeneration.Pdf.Options;
using Granit.DocumentGeneration.Pipeline;
using Granit.Templating.Keys;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PuppeteerSharp;
using PuppeteerSharp.Media;

namespace Granit.DocumentGeneration.Pdf.Internal;

/// <summary>
/// <see cref="IDocumentRenderer"/> implementation that converts HTML to PDF
/// using PuppeteerSharp (headless Chromium).
/// </summary>
internal sealed partial class PuppeteerSharpRenderer(
    ChromiumLifetimeService chromiumLifetime,
    IOptions<PdfRenderOptions> options,
    ILogger<PuppeteerSharpRenderer> logger) : IDocumentRenderer
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
        PdfRenderOptions opts = options.Value;

        await chromiumLifetime.PageSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using IPage page = await chromiumLifetime.Browser.NewPageAsync().ConfigureAwait(false);

            // Block all outbound network requests to prevent SSRF (CWE-918).
            // SetContentAsync injects HTML via CDP — no network request is needed.
            await page.SetRequestInterceptionAsync(true).ConfigureAwait(false);
            page.Request += async (_, e) => await e.Request.AbortAsync().ConfigureAwait(false);

            // Disable JavaScript execution — PDF rendering from HTML/CSS does not
            // require JS, and user-controlled <script> tags could cause CPU/memory
            // exhaustion (CWE-94). SSRF is already blocked by request interception.
            await page.SetJavaScriptEnabledAsync(false).ConfigureAwait(false);

            await page.SetContentAsync(html, new NavigationOptions
            {
                WaitUntil = [WaitUntilNavigation.DOMContentLoaded],
                Timeout = opts.RenderTimeoutMs,
            }).ConfigureAwait(false);

            PdfOptions pdfOptions = new()
            {
                Format = ResolvePaperFormat(opts.PaperFormat),
                Landscape = opts.Landscape,
                PrintBackground = opts.PrintBackground,
                MarginOptions = new MarginOptions
                {
                    Top = opts.MarginTop,
                    Bottom = opts.MarginBottom,
                    Left = opts.MarginLeft,
                    Right = opts.MarginRight,
                },
            };

            if (opts.HeaderTemplate is not null || opts.FooterTemplate is not null)
            {
                pdfOptions.DisplayHeaderFooter = true;
                pdfOptions.HeaderTemplate = opts.HeaderTemplate ?? "<span></span>";
                pdfOptions.FooterTemplate = opts.FooterTemplate ?? "<span></span>";
            }

            byte[] pdfBytes = await page.PdfDataAsync(pdfOptions)
                .WaitAsync(TimeSpan.FromMilliseconds(opts.RenderTimeoutMs), cancellationToken)
                .ConfigureAwait(false);

            LogPdfRendered(pdfBytes.Length, opts.PaperFormat);

            return new DocumentResult(pdfBytes, DocumentFormat.Pdf);
        }
        finally
        {
            chromiumLifetime.PageSemaphore.Release();
        }
    }

    /// <summary>
    /// Resolves a paper format string (e.g. "A4", "Letter") to a <see cref="PaperFormat"/>.
    /// </summary>
    internal static PaperFormat ResolvePaperFormat(string format) =>
        format.ToUpperInvariant() switch
        {
            "A0" => PaperFormat.A0,
            "A1" => PaperFormat.A1,
            "A2" => PaperFormat.A2,
            "A3" => PaperFormat.A3,
            "A4" => PaperFormat.A4,
            "A5" => PaperFormat.A5,
            "A6" => PaperFormat.A6,
            "LETTER" => PaperFormat.Letter,
            "LEGAL" => PaperFormat.Legal,
            "TABLOID" => PaperFormat.Tabloid,
            "LEDGER" => PaperFormat.Ledger,
            _ => PaperFormat.A4,
        };

    [LoggerMessage(Level = LogLevel.Debug, Message = "PDF rendered successfully ({Size} bytes, format={Format})")]
    private partial void LogPdfRendered(int size, string format);
}
