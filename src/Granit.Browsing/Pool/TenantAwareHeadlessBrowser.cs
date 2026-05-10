using System;
using System.Threading;
using System.Threading.Tasks;
using Granit.Browsing.Diagnostics;
using Granit.Browsing.Options;
using Granit.Events;
using Granit.Guids;
using Granit.MultiTenancy;
using Granit.Timing;
using Microsoft.Extensions.Logging;

namespace Granit.Browsing.Pool;

/// <summary>
/// Singleton decorator that wraps the provider-supplied <see cref="IHeadlessBrowser"/>
/// to resolve the current tenant at every <see cref="AcquirePageAsync"/> call, emit
/// <see cref="BrowserPageAcquiredEvent"/>, and tag the page-acquired metric with the
/// correct <c>tenant_id</c>. Closes VULN-106 (tenant attribution).
/// </summary>
/// <remarks>
/// <para>
/// Modelled on <c>Granit.Caching.MultiTenancy.TenantAwareFusionCache</c>:
/// <see cref="ICurrentTenant"/> is backed by <see cref="System.Threading.AsyncLocal{T}"/>
/// so the decorator itself remains a singleton; the tenant is read at call time.
/// </para>
/// <para>
/// Provider packages opt-in by registering the underlying browser as a singleton, then
/// resolving <c>TenantAwareHeadlessBrowser</c> as <see cref="IHeadlessBrowser"/> /
/// <see cref="IHeadlessBrowserPool"/> in F8 / F9.
/// </para>
/// </remarks>
public sealed class TenantAwareHeadlessBrowser : IHeadlessBrowser
{
    private readonly IHeadlessBrowser _inner;
    private readonly ICurrentTenant _currentTenant;
    private readonly BrowsingMetrics _metrics;
    private readonly IGuidGenerator _guidGenerator;
    private readonly ILocalEventBus? _eventBus;
    private readonly IClock _clock;
    private readonly ILogger<TenantAwareHeadlessBrowser> _logger;

    /// <summary>Creates a new tenant-aware decorator over <paramref name="inner"/>.</summary>
    public TenantAwareHeadlessBrowser(
        IHeadlessBrowser inner,
        ICurrentTenant currentTenant,
        BrowsingMetrics metrics,
        IGuidGenerator guidGenerator,
        IClock clock,
        ILogger<TenantAwareHeadlessBrowser> logger,
        ILocalEventBus? eventBus = null)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(currentTenant);
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(guidGenerator);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);

        _inner = inner;
        _currentTenant = currentTenant;
        _metrics = metrics;
        _guidGenerator = guidGenerator;
        _clock = clock;
        _logger = logger;
        _eventBus = eventBus;
    }

    /// <inheritdoc/>
    public string EngineName => _inner.EngineName;

    /// <inheritdoc/>
    public BrowserCapabilities Capabilities => _inner.Capabilities;

    /// <inheritdoc/>
    public bool Supports(BrowserCapabilities capability) => _inner.Supports(capability);

    /// <inheritdoc/>
    public async Task<IBrowserPage> AcquirePageAsync(
        BrowserPageOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        string? tenantId = _currentTenant.IsAvailable
            ? _currentTenant.Id!.Value.ToString("N")
            : null;

        IBrowserPage page = await _inner.AcquirePageAsync(options, cancellationToken).ConfigureAwait(false);

        _metrics.RecordPageAcquired(_inner.EngineName, tenantId);

        if (_eventBus is not null)
        {
            try
            {
                await _eventBus.PublishAsync(
                    new BrowserPageAcquiredEvent(
                        PageId: _guidGenerator.Create(),
                        EngineName: _inner.EngineName,
                        TenantId: tenantId,
                        UserAgent: options?.UserAgent,
                        AcquiredAt: _clock.Now),
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to publish BrowserPageAcquiredEvent.");
            }
        }

        return page;
    }
}
