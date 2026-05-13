using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Granit.Browsing.Diagnostics;
using Granit.Events;
using Granit.Http.Security;
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

        // 1. Scheme.
        if (!IsSchemeAllowed(request.Url.Scheme))
        {
            return await BlockAsync(request, "scheme_not_allowed", cancellationToken).ConfigureAwait(false);
        }

        // 2. Host allow-list.
        if (_sandbox.AllowedHostPatterns is { Count: > 0 } allowed
            && !MatchesAny(allowed, request.Url))
        {
            return await BlockAsync(request, "host_not_allowed", cancellationToken).ConfigureAwait(false);
        }

        // 3. Host deny-list.
        if (_sandbox.DeniedHostPatterns is { Count: > 0 } denied
            && MatchesAny(denied, request.Url))
        {
            return await BlockAsync(request, "host_denied", cancellationToken).ConfigureAwait(false);
        }

        if (_sandbox.BlockedUrlPatterns is { Count: > 0 } blocked
            && MatchesAny(blocked, request.Url))
        {
            return await BlockAsync(request, "url_pattern_blocked", cancellationToken).ConfigureAwait(false);
        }

        // 4. Private-network re-resolution (anti-rebinding).
        if (_sandbox.BlockPrivateNetworks)
        {
            UrlSafetyResult result = await _urlSafety
                .ValidateAsync(request.Url, cancellationToken)
                .ConfigureAwait(false);
            if (!result.IsValid)
            {
                string reason = result.Violation is { Kind: var kind } ? kind.ToString() : "url_safety_violation";
                return await BlockAsync(request, reason, cancellationToken).ConfigureAwait(false);
            }
        }

        // 5. User handlers (registration order, copy-on-write snapshot).
        ImmutableList<Entry> snapshot = Volatile.Read(ref _handlers);
        foreach (Entry entry in snapshot)
        {
            if (!entry.Pattern.IsMatch(request.Url))
            {
                continue;
            }

            try
            {
                RouteDecision decision = await entry.Handler(request, cancellationToken).ConfigureAwait(false);
                if (decision.Kind != RouteDecisionKind.Continue)
                {
                    return decision;
                }
            }
            catch (Exception ex)
            {
                _metrics.RecordRouterHandlerError(_engineName, CurrentTenantId);
                _logger.LogWarning(ex, "User route handler threw for {Url}; applying RouterErrorPolicy={Policy}.", request.Url, _errorPolicy);

                if (_errorPolicy == RouterErrorPolicy.AbortOnError)
                {
                    return RouteDecision.Abort(errorCode: "handler_error");
                }
                // ContinueOnError — fall through to the next handler.
            }
        }

        return RouteDecision.Continue;
    }

    private bool IsSchemeAllowed(string scheme)
    {
        foreach (string allowed in _sandbox.AllowedSchemes)
        {
            if (string.Equals(allowed, scheme, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

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
