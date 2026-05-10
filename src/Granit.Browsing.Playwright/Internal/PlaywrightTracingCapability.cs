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
/// Microsoft.Playwright implementation of <see cref="ITracingCapability"/>. Stages the
/// trace zip into a securely-created temp file (VULN-105) — note that Playwright writes
/// the zip via its own driver process; the file inherits this process's user / ACLs,
/// so the 0600 perms set by <see cref="ITempFileFactory"/> remain effective.
/// </summary>
internal sealed class PlaywrightTracingCapability(ITempFileFactory tempFileFactory) : ITracingCapability
{
    private readonly Dictionary<IBrowserPage, ITempFile> _activeTraces = [];
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

        ITempFile tempFile = await tempFileFactory
            .CreateAsync("trace", "zip", cancellationToken)
            .ConfigureAwait(false);

        lock (_gate)
        {
            _activeTraces[page] = tempFile;
        }
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

        ITempFile? tempFile;
        lock (_gate)
        {
            if (!_activeTraces.TryGetValue(page, out tempFile))
            {
                throw new InvalidOperationException("No active trace for the supplied page — call StartTracingAsync first.");
            }
            _activeTraces.Remove(page);
        }

        try
        {
            await playwrightPage.UnderlyingPage.Context.Tracing
                .StopAsync(new TracingStopOptions { Path = tempFile.Path })
                .ConfigureAwait(false);

            return await File.ReadAllBytesAsync(tempFile.Path, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await tempFile.DisposeAsync().ConfigureAwait(false);
        }
    }
}
