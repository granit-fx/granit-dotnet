using System.Diagnostics;
using Granit.Authorization;
using Granit.Browsing.Capabilities;
using Granit.Browsing.Diagnostics;
using Granit.Browsing.Exceptions;
using Granit.Browsing.Permissions;
using Granit.IO;
using Granit.IO.Options;
using Granit.MultiTenancy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UglyToad.PdfPig;
using BrowsingScreenshotFormat = Granit.Browsing.Options.ScreenshotFormat;
using BrowsingScreenshotOptions = Granit.Browsing.Options.ScreenshotOptions;

namespace Granit.Browsing.Playwright.Internal;

/// <summary>
/// Microsoft.Playwright implementation of <see cref="IPdfViewerCapability"/>
/// (Chromium-only). Loads the PDF in Chromium's built-in PDF viewer via a <c>file://</c>
/// URL pointing at a securely-staged temp file (under the temp-file root) and counts pages with PdfPig
/// instead of the fragile substring heuristic.
/// </summary>
/// <remarks>
/// Registered as a singleton via a factory delegate, so the constructor must not capture
/// <see cref="IPermissionChecker"/> directly (it is scoped, and capturing it would leak
/// the first scope ever resolved). The scope factory is wrapped in
/// <see cref="ScopedPermissionChecker"/>, which creates a fresh DI scope per check.
/// </remarks>
internal sealed partial class PlaywrightPdfViewerCapability : IPdfViewerCapability
{
    private readonly BrowsingMetrics _metrics;
    private readonly ITempFileFactory _tempFileFactory;
    private readonly IOptions<TempFileOptions> _tempFileOptions;
    private readonly IBrowserSandboxProfile _sandbox;
    private readonly ILogger<PlaywrightPdfViewerCapability> _logger;
    private readonly ICurrentTenant? _currentTenant;
    private readonly ScopedPermissionChecker? _permissionChecker;

    public PlaywrightPdfViewerCapability(
        BrowsingMetrics metrics,
        ITempFileFactory tempFileFactory,
        IOptions<TempFileOptions> tempFileOptions,
        IBrowserSandboxProfile sandbox,
        ILogger<PlaywrightPdfViewerCapability> logger,
        ICurrentTenant? currentTenant = null,
        IServiceScopeFactory? scopeFactory = null)
    {
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(tempFileFactory);
        ArgumentNullException.ThrowIfNull(tempFileOptions);
        ArgumentNullException.ThrowIfNull(sandbox);
        ArgumentNullException.ThrowIfNull(logger);

        _metrics = metrics;
        _tempFileFactory = tempFileFactory;
        _tempFileOptions = tempFileOptions;
        _sandbox = sandbox;
        _logger = logger;
        _currentTenant = currentTenant;
        _permissionChecker = ScopedPermissionChecker.TryCreate(scopeFactory);
    }

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

        _ = _sandbox; // referenced via fields; preserved for future profile checks.

        using Activity? activity = BrowsingActivitySource.Source.StartActivity(BrowsingActivitySource.PdfViewerOpen);

        await using MemoryStream buffer = new();
        await pdf.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        byte[] bytes = buffer.ToArray();

        ValidatePdfMagic(bytes);

        int pageCount = CountPages(bytes);

        ITempFile tempFile = await _tempFileFactory
            .CreateAsync("pdf-viewer", "pdf", cancellationToken)
            .ConfigureAwait(false);

        bool fileOwned = true;
        try
        {
            await tempFile.Stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
            await tempFile.Stream.FlushAsync(cancellationToken).ConfigureAwait(false);

            string resolvedPath = Path.GetFullPath(tempFile.Path);
            string root = Path.GetFullPath(
                string.IsNullOrWhiteSpace(_tempFileOptions.Value.RootDirectory)
                    ? Path.Combine(Path.GetTempPath(), "granit")
                    : _tempFileOptions.Value.RootDirectory!);
            if (!resolvedPath.StartsWith(root, StringComparison.Ordinal))
            {
                throw new SandboxViolationException(
                    SandboxViolationKind.HostBlocked,
                    $"Temp PDF path '{resolvedPath}' is outside the temp-file root '{root}'.");
            }

            // Authorize the privileged file:// navigation when an IPermissionChecker is
            // wired. The framework permission `Granit.Browsing.Pages.UseFileScheme`
            // explicitly gates this capability — the PDF viewer is the only path inside
            // the framework that opens a local file in a browser context.
            if (_permissionChecker is not null)
            {
                bool granted = await _permissionChecker
                    .IsGrantedAsync(BrowsingPermissions.Pages.UseFileScheme, cancellationToken)
                    .ConfigureAwait(false);
                if (!granted)
                {
                    throw new UnauthorizedAccessException(
                        $"Permission '{BrowsingPermissions.Pages.UseFileScheme}' is required to open the native PDF viewer.");
                }
            }

            Uri fileUri = new(resolvedPath);

            // Bypass IUrlSafetyValidator for this specific file:// navigation: the path
            // was just resolved against the configured temp-file root above (upstream
            // bound check), the file was created by ITempFileFactory with restrictive
            // permissions, and the global sandbox AllowedSchemes list typically excludes
            // file://. Routing through the validator would either fail closed (breaking
            // the viewer) or require opening file:// globally — both worse than this
            // narrowly-scoped, audited bypass.
            await playwrightPage.UnderlyingPage.GotoAsync(fileUri.ToString()).ConfigureAwait(false);
            await playwrightPage.WaitForLoadStateAsync(LoadState.Load, timeout: TimeSpan.FromSeconds(15),
                cancellationToken).ConfigureAwait(false);

            var result = new PlaywrightPdfDocumentPage(playwrightPage, pageCount, _metrics, tempFile, _logger, _currentTenant);
            fileOwned = false;
            return result;
        }
        finally
        {
            if (fileOwned)
            {
                try
                {
                    await tempFile.DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    LogTempCleanupFailure(ex);
                }
            }
        }
    }

    internal static void ValidatePdfMagic(byte[] bytes)
    {
        const string magic = "%PDF-1.";
        if (bytes.Length < magic.Length + 1)
        {
            throw new InvalidDataException("PDF stream is shorter than the magic-byte header.");
        }
        for (int i = 0; i < magic.Length; i++)
        {
            if (bytes[i] != (byte)magic[i])
            {
                throw new InvalidDataException($"PDF stream does not start with '{magic}x'.");
            }
        }
    }

    private static int CountPages(byte[] bytes)
    {
        using var doc = PdfDocument.Open(bytes);
        return doc.NumberOfPages;
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Granit.Browsing.Playwright failed to clean up a staged PDF temp file.")]
    private partial void LogTempCleanupFailure(Exception exception);
}

internal sealed partial class PlaywrightPdfDocumentPage(
    PlaywrightBrowserPage page,
    int pageCount,
    BrowsingMetrics metrics,
    ITempFile tempFile,
    ILogger logger,
    ICurrentTenant? currentTenant) : IPdfDocumentPage
{
    private string? CurrentTenantId =>
        currentTenant is { IsAvailable: true } t ? t.Id?.ToString("N") : null;

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
        // pageIndex is a server-validated int (ArgumentOutOfRangeException above) — no
        // user-controllable script payload reaches EvaluateAsync. Suppress GRBROWSING001
        // which can't see the upstream bound check.
        string pageHashScript = "(async () => { try { window.location.hash = '#page=" +
            (pageIndex + 1).ToString(System.Globalization.CultureInfo.InvariantCulture) +
            "'; return true; } catch { return false; } })()";
#pragma warning disable GRBROWSING001
        await page.EvaluateAsync<bool>(pageHashScript, cancellationToken).ConfigureAwait(false);
#pragma warning restore GRBROWSING001
        await Task.Delay(TimeSpan.FromMilliseconds(150), cancellationToken).ConfigureAwait(false);

        byte[] png = await page.ScreenshotAsync(new BrowsingScreenshotOptions
        {
            Format = BrowsingScreenshotFormat.Png,
        }, cancellationToken).ConfigureAwait(false);

        metrics.RecordRenderDuration(page.EngineName, CurrentTenantId, "pdf_viewer_render_page", sw.Elapsed);
        return png;
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        try
        {
            await tempFile.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogTempCleanupFailure(logger, ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Granit.Browsing.Playwright failed to clean up a staged PDF temp file on viewer disposal.")]
    private static partial void LogTempCleanupFailure(ILogger logger, Exception exception);
}
