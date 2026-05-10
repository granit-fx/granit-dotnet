using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Granit.Browsing.Options;

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
    Task NavigateAsync(string url, NavigationOptions? options = null, CancellationToken cancellationToken = default);

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
    /// Routes requests matching <paramref name="urlPattern"/> through <paramref name="handler"/>.
    /// Used to block tracker domains, mock API responses for offline rendering, or sandbox
    /// untrusted content.
    /// </summary>
    /// <remarks>Requires <see cref="BrowserCapabilities.NetworkInterception"/> on the parent browser.</remarks>
    Task RouteAsync(string urlPattern, RouteHandler handler, CancellationToken cancellationToken = default);

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

/// <summary>
/// Asynchronous handler for an intercepted request. Implementations choose to fulfill,
/// abort, continue, or redirect the request through the provider-neutral
/// <see cref="IRouteContext"/> surface.
/// </summary>
/// <param name="context">Operation surface tied to the intercepted request.</param>
/// <param name="cancellationToken">Cancellation token.</param>
public delegate Task RouteHandler(IRouteContext context, CancellationToken cancellationToken);

/// <summary>Operation surface for a single intercepted request handled by a <see cref="RouteHandler"/>.</summary>
public interface IRouteContext
{
    /// <summary>The request URL.</summary>
    string Url { get; }

    /// <summary>The HTTP method (GET / POST / …).</summary>
    string Method { get; }

    /// <summary>Request headers (case-insensitive lookup is the provider's responsibility).</summary>
    IReadOnlyDictionary<string, string> Headers { get; }

    /// <summary>Lets the request continue to the network unmodified.</summary>
    Task ContinueAsync(CancellationToken cancellationToken = default);

    /// <summary>Aborts the request with the supplied error string (provider-defined codes).</summary>
    Task AbortAsync(string errorCode = "failed", CancellationToken cancellationToken = default);

    /// <summary>Fulfills the request with a synthetic response.</summary>
    Task FulfillAsync(int statusCode, IReadOnlyDictionary<string, string>? headers, byte[]? body,
        CancellationToken cancellationToken = default);
}

/// <summary>A console message observed in the rendered page.</summary>
/// <param name="Type">Console method invoked: <c>log</c>, <c>info</c>, <c>warn</c>, <c>error</c>, <c>debug</c>.</param>
/// <param name="Text">The text representation of the arguments.</param>
public sealed record ConsoleMessage(string Type, string Text);

/// <summary>An uncaught JavaScript error observed in the rendered page.</summary>
/// <param name="Message">The error message.</param>
/// <param name="StackTrace">Engine-formatted stack trace, when available.</param>
public sealed record PageError(string Message, string? StackTrace);
