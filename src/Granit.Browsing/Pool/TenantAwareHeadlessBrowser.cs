using Granit.Authorization;
using Granit.Browsing.Diagnostics;
using Granit.Browsing.Options;
using Granit.Browsing.Pages;
using Granit.Browsing.Permissions;
using Granit.Events;
using Granit.Guids;
using Granit.MultiTenancy;
using Granit.Timing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Granit.Browsing.Pool;

/// <summary>
/// Singleton decorator that wraps the provider-supplied <see cref="IHeadlessBrowser"/>
/// to resolve the current tenant at every <see cref="AcquirePageAsync"/> call, emit
/// <see cref="BrowserPageAcquiredEvent"/>, and tag the page-acquired metric with the
/// correct <c>tenant_id</c>, guaranteeing per-tenant attribution on every acquisition.
/// When an <see cref="IPermissionChecker"/> is registered, also enforces
/// <see cref="BrowsingPermissions.Pages.Acquire"/> at acquisition time and wraps the
/// returned page with a <see cref="PermissionAwareBrowserPage"/> that gates Navigate /
/// InjectScript / SetContent through the same checker.
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
/// <para>
/// <see cref="ILocalEventBus"/> and <see cref="IPermissionChecker"/> are both scoped in
/// the framework, so the decorator can't capture them directly without tripping
/// <c>ValidateScopes</c> (and, worse, leaking the first scope ever resolved across all
/// subsequent requests). The constructor therefore accepts an
/// <see cref="IServiceScopeFactory"/> and routes each publish / permission check through
/// <see cref="ScopedLocalEventBus"/> / <see cref="ScopedPermissionChecker"/>, which
/// create a fresh DI scope per call.
/// </para>
/// </remarks>
public sealed class TenantAwareHeadlessBrowser : IHeadlessBrowser
{
    private readonly IHeadlessBrowser _inner;
    private readonly ICurrentTenant _currentTenant;
    private readonly BrowsingMetrics _metrics;
    private readonly IGuidGenerator _guidGenerator;
    private readonly ScopedLocalEventBus? _eventBus;
    private readonly ScopedPermissionChecker? _permissionChecker;
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
        IServiceScopeFactory? scopeFactory = null)
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
        _eventBus = ScopedLocalEventBus.TryCreate(scopeFactory);
        _permissionChecker = ScopedPermissionChecker.TryCreate(scopeFactory);
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
        if (_permissionChecker is not null)
        {
            bool granted = await _permissionChecker
                .IsGrantedAsync(BrowsingPermissions.Pages.Acquire, cancellationToken)
                .ConfigureAwait(false);
            if (!granted)
            {
                throw new UnauthorizedAccessException(
                    $"Permission '{BrowsingPermissions.Pages.Acquire}' is required to acquire a browser page.");
            }
        }

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

        return _permissionChecker is not null
            ? new PermissionAwareBrowserPage(page, _permissionChecker)
            : page;
    }
}
