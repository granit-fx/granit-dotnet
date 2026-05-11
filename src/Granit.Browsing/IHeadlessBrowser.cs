using Granit.Browsing.Options;

namespace Granit.Browsing;

/// <summary>
/// Provider-agnostic facade for a headless browser engine. Producing this object
/// is the responsibility of provider packages such as <c>Granit.Browsing.PuppeteerSharp</c>
/// or <c>Granit.Browsing.Playwright</c>; hosts inject this contract everywhere they need
/// browser-driven rendering (HTML → PDF, page screenshots, PDF viewer rendering, etc.).
/// </summary>
/// <remarks>
/// <para>
/// Providers MUST advertise the set of features they support through
/// <see cref="Capabilities"/>. Optional capabilities (<see cref="Capabilities.IPdfCapability"/>,
/// <see cref="Capabilities.IPdfViewerCapability"/>, etc.) are exposed as separate
/// interfaces — registered in the DI container only when the corresponding flag is set on
/// <see cref="Capabilities"/>. A consumer that requires <c>IPdfCapability</c> simply
/// constructor-injects it; if the active provider does not advertise
/// <see cref="BrowserCapabilities.PdfGeneration"/>, the host fails at boot with a
/// descriptive missing-service error.
/// </para>
/// <para>
/// Implementations are typically singleton — they own a long-lived browser pool. Pages
/// are short-lived and acquired through <see cref="AcquirePageAsync"/> which returns an
/// <see cref="IBrowserPage"/> whose <see cref="System.IAsyncDisposable"/> implementation
/// returns the underlying engine page to the pool (or recycles it).
/// </para>
/// </remarks>
public interface IHeadlessBrowser
{
    /// <summary>
    /// Engine identifier — used for diagnostics and capability matrix logging. Convention:
    /// <c>{engine}-{provider}</c> (e.g. <c>"chromium-puppeteer"</c>,
    /// <c>"chromium-playwright"</c>, <c>"firefox-playwright"</c>).
    /// </summary>
    string EngineName { get; }

    /// <summary>
    /// Capability flags advertised by this provider. Consumers gate on this set or use
    /// <see cref="Supports"/>.
    /// </summary>
    BrowserCapabilities Capabilities { get; }

    /// <summary>
    /// Returns <c>true</c> when every flag in <paramref name="capability"/> is advertised
    /// by <see cref="Capabilities"/>. Pass a single flag for a single check or combine
    /// flags to require all of them.
    /// </summary>
    bool Supports(BrowserCapabilities capability);

    /// <summary>
    /// Acquires a page from the underlying pool. Disposing the returned
    /// <see cref="IBrowserPage"/> returns it to the pool; the pool may recycle the
    /// browser context (close it cleanly, open a fresh one) according to its
    /// configured lifetime / max-pages policy.
    /// </summary>
    /// <param name="options">
    /// Optional per-page configuration: viewport, user agent, locale, timezone, cookies,
    /// emulated media type. <c>null</c> uses provider defaults.
    /// </param>
    /// <param name="cancellationToken">Cancels the acquisition wait — see <c>GranitBrowsingOptions.AcquireTimeout</c>.</param>
    /// <returns>A page handle. Always dispose it (preferably with <c>await using</c>).</returns>
    /// <exception cref="System.TimeoutException">When the acquisition exceeds the configured wait window.</exception>
    System.Threading.Tasks.Task<IBrowserPage> AcquirePageAsync(
        BrowserPageOptions? options = null,
        System.Threading.CancellationToken cancellationToken = default);
}
