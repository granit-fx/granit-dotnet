using System;
using System.ComponentModel.DataAnnotations;

namespace Granit.Browsing.Options;

/// <summary>Options binding the pool sizing and lifetime behaviour.</summary>
/// <remarks>
/// Hosts override per-tenant tier or per-deployment via the standard
/// <c>Microsoft.Extensions.Options</c> pipeline. Provider packages bind to the same
/// section to keep configuration uniform across PuppeteerSharp / Playwright.
/// </remarks>
public sealed class GranitBrowsingOptions
{
    /// <summary>Configuration section key (<c>"Browsing"</c>).</summary>
    public const string SectionName = "Browsing";

    /// <summary>
    /// Maximum number of browser instances held simultaneously by the pool. Each
    /// browser carries roughly 200–300 MB of resident memory; default is 2 to balance
    /// throughput and footprint on a single-host deployment.
    /// </summary>
    [Range(1, 64)]
    public int MaxBrowsers { get; set; } = 2;

    /// <summary>
    /// Maximum number of pages multiplexed onto a single browser instance before the
    /// pool spins up another. Defaults to 8.
    /// </summary>
    [Range(1, 256)]
    public int MaxPagesPerBrowser { get; set; } = 8;

    /// <summary>
    /// How long a page can sit idle in the pool before it is recycled (closed and
    /// re-opened). Mitigates accumulating browser memory leaks. Default 5 minutes.
    /// </summary>
    [Range(typeof(TimeSpan), "00:00:30", "01:00:00")]
    public TimeSpan PageIdleTimeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Hard maximum lifetime of a page before it is recycled, regardless of idleness.
    /// Default 30 minutes.
    /// </summary>
    [Range(typeof(TimeSpan), "00:01:00", "06:00:00")]
    public TimeSpan PageMaxLifetime { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Maximum time <see cref="IHeadlessBrowser.AcquirePageAsync"/> may block waiting
    /// for an available page. Default 30 seconds.
    /// </summary>
    [Range(typeof(TimeSpan), "00:00:01", "00:05:00")]
    public TimeSpan AcquireTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Maximum time <see cref="IHeadlessBrowserPool.DrainAsync"/> may wait for in-flight
    /// pages to be released before force-disposing the underlying browsers. Default 30
    /// seconds. Ensures shutdown cannot block indefinitely on a misbehaving page handle.
    /// </summary>
    [Range(typeof(TimeSpan), "00:00:05", "00:05:00")]
    public TimeSpan DrainTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Per-page resource limits enforced by the provider where supported.</summary>
    public ResourceLimits ResourceLimits { get; set; } = new();
}

/// <summary>Per-page resource limits.</summary>
public sealed class ResourceLimits
{
    /// <summary>
    /// Hard timeout for any single render operation (navigate, screenshot, PDF, etc.).
    /// Defaults to 30 seconds — surfaces as a <see cref="TimeoutException"/> on the
    /// caller. <c>null</c> disables the cap.
    /// </summary>
    public TimeSpan? MaxRenderDuration { get; set; } = TimeSpan.FromSeconds(30);
}
