using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Granit.Browsing.Capabilities;
using Granit.IO;
using Microsoft.Playwright;

namespace Granit.Browsing.Playwright.Internal;

/// <summary>
/// Microsoft.Playwright implementation of <see cref="IHarRecordingCapability"/>. Records
/// network activity through a per-page <c>RouteFromHARAsync</c> session staged to a
/// securely-created temp file and serialises it to a JSON HAR document on stop.
/// </summary>
internal sealed class PlaywrightHarRecordingCapability(ITempFileFactory tempFileFactory) : IHarRecordingCapability
{
    private readonly Dictionary<IBrowserPage, ITempFile> _activeRecordings = [];
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

        ITempFile tempFile = await tempFileFactory
            .CreateAsync("har", "har", cancellationToken)
            .ConfigureAwait(false);

        lock (_gate)
        {
            _activeRecordings[page] = tempFile;
        }

        await playwrightPage.UnderlyingPage.Context.RouteFromHARAsync(tempFile.Path, new BrowserContextRouteFromHAROptions
        {
            Update = true,
            UpdateMode = HarMode.Full,
        }).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<string> StopHarAsync(IBrowserPage page, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(page);
        ITempFile? tempFile;
        lock (_gate)
        {
            if (!_activeRecordings.TryGetValue(page, out tempFile))
            {
                throw new InvalidOperationException("No active HAR recording for the supplied page — call StartHarAsync first.");
            }
            _activeRecordings.Remove(page);
        }

        try
        {
            tempFile.Stream.Position = 0;
            using StreamReader reader = new(tempFile.Stream, leaveOpen: true);
            string content = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrEmpty(content))
            {
                return "{\"log\":{\"version\":\"1.2\",\"entries\":[]}}";
            }
            return content;
        }
        finally
        {
            await tempFile.DisposeAsync().ConfigureAwait(false);
        }
    }
}
