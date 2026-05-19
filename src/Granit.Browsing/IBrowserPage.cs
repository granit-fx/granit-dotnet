using Granit.Browsing.Options;
using Granit.Browsing.Pages;

namespace Granit.Browsing;

/// <summary>
/// A single page inside a headless browser, surfaced as a provider-neutral abstraction.
/// Disposing the page returns it to the pool.
/// </summary>
/// <remarks>
/// <para>
/// Every operation supported here is universal across the engines we target — Chromium
/// (PuppeteerSharp + Playwright), Firefox, WebKit. Engine-specific functionality lives in
/// dedicated capability interfaces (<see cref="Capabilities.IPdfCapability"/>,
/// <see cref="Capabilities.IPdfViewerCapability"/>, <see cref="Capabilities.ITracingCapability"/>,
/// etc.) consumed alongside <c>IBrowserPage</c> when the provider advertises them.
/// </para>
/// </remarks>
public interface IBrowserPage : IAsyncDisposable
{
    /// <summary>
    /// Engine name of the parent <see cref="IHeadlessBrowser"/> — exposed here as a
    /// convenience so callers can branch on engine without re-injecting the browser.
    /// </summary>
    string EngineName { get; }

    /// <summary>Navigates the page to <paramref name="url"/>.</summary>
    /// <remarks>
    /// Providers validate the URL through <c>Granit.Http.Security.IUrlSafetyValidator</c>
    /// before issuing the engine request. A blocked URL surfaces as
    /// <see cref="Exceptions.SandboxViolationException"/>.
    /// </remarks>
    Task NavigateAsync(Uri url, NavigationOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces the page's document with <paramref name="html"/>. Useful for HTML→PDF and
    /// HTML→screenshot flows where the source HTML is generated server-side.
    /// </summary>
    Task SetContentAsync(string html, NavigationOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>Returns the currently loaded URL (after redirects).</summary>
    Task<string> GetCurrentUrlAsync(CancellationToken cancellationToken = default);

    /// <summary>Waits for the page to reach <paramref name="state"/>.</summary>
    Task WaitForLoadStateAsync(LoadState state, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

    /// <summary>Waits for a CSS selector to appear in the DOM.</summary>
    Task WaitForSelectorAsync(string selector, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

    /// <summary>Waits for a JavaScript predicate to evaluate to a truthy value.</summary>
    Task WaitForFunctionAsync(string jsExpression, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

    /// <summary>Evaluates a JavaScript expression in the page context and returns its result.</summary>
    Task<TResult?> EvaluateAsync<TResult>(string jsExpression, CancellationToken cancellationToken = default);

    /// <summary>Injects a <c>&lt;style&gt;</c> tag with the supplied CSS into the rendered document.</summary>
    Task AddStyleTagAsync(string css, CancellationToken cancellationToken = default);

    /// <summary>Injects a <c>&lt;script&gt;</c> tag with the supplied JavaScript into the rendered document.</summary>
    Task AddScriptTagAsync(string js, CancellationToken cancellationToken = default);

    /// <summary>Captures a screenshot of the page using the provider's encoder.</summary>
    /// <remarks>Requires <see cref="BrowserCapabilities.Screenshot"/> on the parent browser — universal in practice.</remarks>
    Task<byte[]> ScreenshotAsync(ScreenshotOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers <paramref name="handler"/> against <paramref name="pattern"/>. When an
    /// intercepted request matches, the handler returns a <see cref="RouteDecision"/> the
    /// provider applies to the engine-native request.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Multiple handlers may be registered; the request router evaluates them in
    /// registration order after every sandbox rule has been applied. Sandbox always wins —
    /// a handler can refine policy but never widen it.
    /// </para>
    /// <para>
    /// Requires <see cref="BrowserCapabilities.NetworkInterception"/> on the parent
    /// browser.
    /// </para>
    /// </remarks>
    Task RouteAsync(
        RoutePattern pattern,
        Func<RouteRequest, CancellationToken, ValueTask<RouteDecision>> handler,
        CancellationToken cancellationToken = default);

    /// <summary>Console messages observed while the page was alive (info/warn/error).</summary>
    IObservable<ConsoleMessage> ConsoleMessages { get; }

    /// <summary>Page-level JavaScript errors observed while the page was alive.</summary>
    IObservable<PageError> PageErrors { get; }
}

/// <summary>Phase the page-load pipeline reaches before a wait completes.</summary>
public enum LoadState
{
    /// <summary>The <c>load</c> DOM event has fired.</summary>
    Load,

    /// <summary>The <c>DOMContentLoaded</c> event has fired (parsed but resources may still load).</summary>
    DomContentLoaded,

    /// <summary>No network requests for at least 500 ms (engine-defined idle window).</summary>
    NetworkIdle,
}

/// <summary>A console message observed in the rendered page.</summary>
/// <param name="Type">Console method invoked: <c>log</c>, <c>info</c>, <c>warn</c>, <c>error</c>, <c>debug</c>.</param>
/// <param name="Text">The text representation of the arguments.</param>
public sealed record ConsoleMessage(string Type, string Text);

/// <summary>An uncaught JavaScript error observed in the rendered page.</summary>
/// <param name="Message">The error message.</param>
/// <param name="StackTrace">Engine-formatted stack trace, when available.</param>
public sealed record PageError(string Message, string? StackTrace);
