using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Granit.Browsing.Capabilities;
using Microsoft.Playwright;

namespace Granit.Browsing.Playwright.Internal;

/// <summary>Microsoft.Playwright implementation of <see cref="ITracingCapability"/>.</summary>
internal sealed class PlaywrightTracingCapability : ITracingCapability
{
    private readonly Dictionary<IBrowserPage, string> _activeTraces = new();
    private readonly object _gate = new();

    /// <inheritdoc/>
    public async Task StartTracingAsync(IBrowserPage page, TracingOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(options);
        if (page is not PlaywrightBrowserPage playwrightPage)
        {
            throw new InvalidOperationException(
                $"PlaywrightTracingCapability requires a page produced by {nameof(PlaywrightHeadlessBrowser)}; got {page.GetType().Name}.");
        }

        IBrowserContext context = playwrightPage.UnderlyingPage.Context;
        await context.Tracing.StartAsync(new TracingStartOptions
        {
            Snapshots = options.Snapshots,
            Screenshots = options.Screenshots,
            Sources = options.Sources,
            Title = options.Title,
        }).ConfigureAwait(false);

        string tempPath = Path.Combine(Path.GetTempPath(), $"granit-trace-{Path.GetRandomFileName()}.zip");
        lock (_gate)
        {
            _activeTraces[page] = tempPath;
        }
        _ = cancellationToken;
    }

    /// <inheritdoc/>
    public async Task<byte[]> StopTracingAsync(IBrowserPage page, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(page);
        if (page is not PlaywrightBrowserPage playwrightPage)
        {
            throw new InvalidOperationException(
                $"PlaywrightTracingCapability requires a page produced by {nameof(PlaywrightHeadlessBrowser)}; got {page.GetType().Name}.");
        }

        string? path;
        lock (_gate)
        {
            if (!_activeTraces.TryGetValue(page, out path))
            {
                throw new InvalidOperationException("No active trace for the supplied page — call StartTracingAsync first.");
            }
            _activeTraces.Remove(page);
        }

        try
        {
            await playwrightPage.UnderlyingPage.Context.Tracing
                .StopAsync(new TracingStopOptions { Path = path })
                .ConfigureAwait(false);

            return await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (File.Exists(path))
            {
                try { File.Delete(path); } catch { /* best-effort */ }
            }
        }
    }
}
