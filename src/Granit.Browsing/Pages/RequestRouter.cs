using System.Collections.Immutable;
using Granit.Browsing.Diagnostics;
using Granit.Events;
using Granit.Http.UrlSafety;
using Granit.MultiTenancy;
using Granit.Timing;
using Microsoft.Extensions.Logging;

namespace Granit.Browsing.Pages;

/// <summary>
/// Default <see cref="IRequestRouter"/> — composes sandbox rules and user-registered
/// handlers into a single ordered policy chain.
/// </summary>
/// <remarks>
/// <para>Order of evaluation:</para>
/// <list type="number">
///   <item>Scheme allowlist (<see cref="IBrowserSandboxProfile.AllowedSchemes"/>).</item>
///   <item>Host allow-list (<see cref="IBrowserSandboxProfile.AllowedHostPatterns"/>).</item>
///   <item>Host deny-list and blocked URL patterns (<see cref="IBrowserSandboxProfile.DeniedHostPatterns"/> + <see cref="IBrowserSandboxProfile.BlockedUrlPatterns"/>).</item>
///   <item>Private-network re-resolution via <see cref="IUrlSafetyValidator"/> when <see cref="IBrowserSandboxProfile.BlockPrivateNetworks"/> is set.</item>
///   <item>User-registered handlers (in registration order). The first non-Continue decision wins.</item>
///   <item>Default <see cref="RouteDecision.Continue"/>.</item>
/// </list>
/// <para>
/// Handlers are stored in a copy-on-write <see cref="ImmutableList{T}"/> and capped at
/// <see cref="MaxHandlers"/> entries per page. Each <see cref="Register"/> call returns
/// an <see cref="IDisposable"/> token; disposing the token removes the handler.
/// </para>
/// <para>
/// User-handler exceptions are subject to the configured <see cref="RouterErrorPolicy"/>:
/// <see cref="RouterErrorPolicy.AbortOnError"/> (default) fails the request closed;
/// <see cref="RouterErrorPolicy.ContinueOnError"/> skips the throwing handler. Sandbox
/// rules ALWAYS evaluate first — no user code can widen them.
/// </para>
/// </remarks>
internal sealed class RequestRouter : IRequestRouter
{
    /// <summary>Hard cap on the number of user-registered handlers per page.</summary>
    public const int MaxHandlers = 64;

    private readonly IBrowserSandboxProfile _sandbox;
    private readonly IUrlSafetyValidator _urlSafety;
    private readonly BrowsingMetrics _metrics;
    private readonly ILocalEventBus? _eventBus;
    private readonly ICurrentTenant? _currentTenant;
    private readonly ILogger<RequestRouter> _logger;
    private readonly IClock _clock;
    private readonly string _engineName;
    private readonly Guid _pageId;
    private readonly RouterErrorPolicy _errorPolicy;

    private ImmutableList<Entry> _handlers = ImmutableList<Entry>.Empty;

    public RequestRouter(
        IBrowserSandboxProfile sandbox,
        IUrlSafetyValidator urlSafety,
        BrowsingMetrics metrics,
        ILogger<RequestRouter> logger,
        IClock clock,
        string engineName,
        Guid pageId,
        ILocalEventBus? eventBus = null,
        ICurrentTenant? currentTenant = null,
        RouterErrorPolicy errorPolicy = RouterErrorPolicy.AbortOnError)
    {
        ArgumentNullException.ThrowIfNull(sandbox);
        ArgumentNullException.ThrowIfNull(urlSafety);
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentException.ThrowIfNullOrEmpty(engineName);

        _sandbox = sandbox;
        _urlSafety = urlSafety;
        _metrics = metrics;
        _logger = logger;
        _clock = clock;
        _engineName = engineName;
        _pageId = pageId;
        _eventBus = eventBus;
        _currentTenant = currentTenant;
        _errorPolicy = errorPolicy;
    }

    private string? CurrentTenantId =>
        _currentTenant is { IsAvailable: true } t ? t.Id?.ToString("N") : null;

    /// <inheritdoc/>
    public IDisposable Register(RoutePattern pattern, Func<RouteRequest, CancellationToken, ValueTask<RouteDecision>> handler)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        ArgumentNullException.ThrowIfNull(handler);

        Entry entry = new(pattern, handler);

        while (true)
        {
            ImmutableList<Entry> current = Volatile.Read(ref _handlers);
            if (current.Count >= MaxHandlers)
            {
                throw new InvalidOperationException(
                    $"RequestRouter handler cap ({MaxHandlers}) reached — dispose unused registrations before adding new ones.");
            }
            ImmutableList<Entry> next = current.Add(entry);
            if (Interlocked.CompareExchange(ref _handlers, next, current) == current)
            {
                return new Subscription(this, entry);
            }
        }
    }

    private void Unregister(Entry entry)
    {
        while (true)
        {
            ImmutableList<Entry> current = Volatile.Read(ref _handlers);
            ImmutableList<Entry> next = current.Remove(entry);
            if (ReferenceEquals(current, next))
            {
                return;
            }
            if (Interlocked.CompareExchange(ref _handlers, next, current) == current)
            {
                return;
            }
        }
    }

    /// <inheritdoc/>
    public async ValueTask<RouteDecision> EvaluateAsync(RouteRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // 1-3. Sandbox shape checks (scheme, host allow/deny, URL pattern block) are all sync.
        if (CheckSandboxRules(request) is { } sandboxReason)
        {
            return await BlockAsync(request, sandboxReason, cancellationToken).ConfigureAwait(false);
        }

        // 4. Private-network re-resolution (anti-rebinding) — async because it goes through DNS.
        if (await CheckPrivateNetworkBlockAsync(request, cancellationToken).ConfigureAwait(false) is { } pnReason)
        {
            return await BlockAsync(request, pnReason, cancellationToken).ConfigureAwait(false);
        }

        // 5. User handlers (registration order, copy-on-write snapshot).
        return await RunUserHandlersAsync(request, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Synchronous sandbox-shape checks: scheme allowlist, host allow/deny patterns,
    /// blocked URL patterns. Returns a metric reason on block, or <see langword="null"/> on pass.
    /// </summary>
    private string? CheckSandboxRules(RouteRequest request)
    {
        if (!IsSchemeAllowed(request.Url.Scheme))
        {
            return "scheme_not_allowed";
        }

        if (_sandbox.AllowedHostPatterns is { Count: > 0 } allowed
            && !MatchesAny(allowed, request.Url))
        {
            return "host_not_allowed";
        }

        if (_sandbox.DeniedHostPatterns is { Count: > 0 } denied
            && MatchesAny(denied, request.Url))
        {
            return "host_denied";
        }

        if (_sandbox.BlockedUrlPatterns is { Count: > 0 } blocked
            && MatchesAny(blocked, request.Url))
        {
            return "url_pattern_blocked";
        }

        return null;
    }

    private async ValueTask<string?> CheckPrivateNetworkBlockAsync(RouteRequest request, CancellationToken cancellationToken)
    {
        if (!_sandbox.BlockPrivateNetworks)
        {
            return null;
        }

        UrlSafetyResult result = await _urlSafety
            .ValidateAsync(request.Url, cancellationToken)
            .ConfigureAwait(false);
        if (result.IsValid)
        {
            return null;
        }

        return result.Violation is { Kind: var kind } ? kind.ToString() : "url_safety_violation";
    }

    private async ValueTask<RouteDecision> RunUserHandlersAsync(RouteRequest request, CancellationToken cancellationToken)
    {
        ImmutableList<Entry> snapshot = Volatile.Read(ref _handlers);
        foreach (Entry entry in snapshot)
        {
            if (!entry.Pattern.IsMatch(request.Url))
            {
                continue;
            }

            RouteDecision? terminal = await InvokeHandlerAsync(entry, request, cancellationToken).ConfigureAwait(false);
            if (terminal is not null)
            {
                return terminal;
            }
        }

        return RouteDecision.Continue;
    }

    /// <summary>
    /// Runs one user handler. Returns the decision when terminal (non-Continue),
    /// <see langword="null"/> when the loop should advance. Handler exceptions are mapped
    /// to <see cref="RouterErrorPolicy"/>: <c>AbortOnError</c> returns an Abort decision,
    /// <c>ContinueOnError</c> returns <see langword="null"/> to advance to the next handler.
    /// </summary>
    private async ValueTask<RouteDecision?> InvokeHandlerAsync(Entry entry, RouteRequest request, CancellationToken cancellationToken)
    {
        try
        {
            RouteDecision decision = await entry.Handler(request, cancellationToken).ConfigureAwait(false);
            return decision.Kind == RouteDecisionKind.Continue ? null : decision;
        }
        catch (Exception ex)
        {
            _metrics.RecordRouterHandlerError(_engineName, CurrentTenantId);
            _logger.LogWarning(ex, "User route handler threw for {Url}; applying RouterErrorPolicy={Policy}.", request.Url, _errorPolicy);

            return _errorPolicy == RouterErrorPolicy.AbortOnError
                ? RouteDecision.Abort(errorCode: "handler_error")
                : null;
        }
    }

    private bool IsSchemeAllowed(string scheme) =>
        _sandbox.AllowedSchemes.Any(allowed => string.Equals(allowed, scheme, StringComparison.OrdinalIgnoreCase));

    private static bool MatchesAny(IReadOnlyList<string> patterns, Uri url)
    {
        // Defers to RoutePattern so sandbox host/URL patterns share the same anchored
        // host + path matcher used by user-registered handlers. Substring matches on
        // "{host}{path}" via FileSystemGlobbing.Matcher leak across the host boundary
        // (e.g. evil.com/api.partner.com/exfil would otherwise match api.partner.com/**).
        for (int i = 0; i < patterns.Count; i++)
        {
            if (RoutePattern.Parse(patterns[i]).IsMatch(url))
            {
                return true;
            }
        }
        return false;
    }

    private async Task<RouteDecision> BlockAsync(RouteRequest request, string reason, CancellationToken cancellationToken)
    {
        string? tenantId = CurrentTenantId;
        _metrics.RecordError(_engineName, tenantId, $"sandbox.{reason}");

        if (_eventBus is not null)
        {
            try
            {
                await _eventBus.PublishAsync(
                    new BrowserUrlNavigatedEvent(
                        _pageId,
                        _engineName,
                        request.Url.ToString(),
                        BlockedBySandbox: true,
                        BlockReason: reason,
                        NavigatedAt: _clock.Now),
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to publish BrowserUrlNavigatedEvent on sandbox block.");
            }
        }

        return RouteDecision.Abort(errorCode: reason);
    }

    private sealed record Entry(RoutePattern Pattern, Func<RouteRequest, CancellationToken, ValueTask<RouteDecision>> Handler);

    private sealed class Subscription(RequestRouter owner, Entry entry) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                owner.Unregister(entry);
            }
        }
    }
}
