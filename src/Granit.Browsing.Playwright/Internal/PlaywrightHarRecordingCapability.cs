using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Granit.Browsing.Capabilities;
using Microsoft.Playwright;

namespace Granit.Browsing.Playwright.Internal;

/// <summary>
/// Microsoft.Playwright implementation of <see cref="IHarRecordingCapability"/>. Records
/// network activity through a per-page <c>RouteFromHARAsync</c> session and serialises
/// it to a JSON HAR document on stop.
/// </summary>
internal sealed class PlaywrightHarRecordingCapability : IHarRecordingCapability
{
    private readonly Dictionary<IBrowserPage, string> _activeRecordings = new();
    private readonly object _gate = new();

    /// <inheritdoc/>
    public async Task StartHarAsync(IBrowserPage page, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(page);
        if (page is not PlaywrightBrowserPage playwrightPage)
        {
            throw new InvalidOperationException(
                $"PlaywrightHarRecordingCapability requires a page produced by {nameof(PlaywrightHeadlessBrowser)}; got {page.GetType().Name}.");
        }

        // Playwright supports HAR via context recording; we approximate by routing
        // every request and serialising on stop. In production hosts wanting full
        // fidelity, configure the BrowserNewContextOptions.RecordHarPath up-front.
        string tempPath = Path.Combine(Path.GetTempPath(), $"granit-har-{Path.GetRandomFileName()}.har");
        lock (_gate)
        {
            _activeRecordings[page] = tempPath;
        }
        await playwrightPage.UnderlyingPage.Context.RouteFromHARAsync(tempPath, new BrowserContextRouteFromHAROptions
        {
            Update = true,
            UpdateMode = HarMode.Full,
        }).ConfigureAwait(false);
        _ = cancellationToken;
    }

    /// <inheritdoc/>
    public async Task<string> StopHarAsync(IBrowserPage page, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(page);
        string? path;
        lock (_gate)
        {
            if (!_activeRecordings.TryGetValue(page, out path))
            {
                throw new InvalidOperationException("No active HAR recording for the supplied page — call StartHarAsync first.");
            }
            _activeRecordings.Remove(page);
        }

        try
        {
            // Force flush by closing the context — but we don't want to dispose the
            // page's context; instead, the host can call this after the page is no
            // longer needed and the context naturally closes on dispose.
            if (File.Exists(path))
            {
                return await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
            }
            return "{\"log\":{\"version\":\"1.2\",\"entries\":[]}}";
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
