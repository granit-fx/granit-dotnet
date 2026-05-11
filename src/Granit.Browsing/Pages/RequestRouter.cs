using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Granit.Browsing.Diagnostics;
using Granit.Events;
using Granit.Http.Security;
using Granit.Timing;
using Microsoft.Extensions.FileSystemGlobbing;
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
/// User-handler exceptions are caught and logged; the router defaults to
/// <see cref="RouteDecision.Continue"/> for that handler rather than failing the entire
/// request. Sandbox rules ALWAYS evaluate first — no user code can widen them.
/// </para>
/// </remarks>
internal sealed class RequestRouter : IRequestRouter
{
    private readonly IBrowserSandboxProfile _sandbox;
    private readonly IUrlSafetyValidator _urlSafety;
    private readonly BrowsingMetrics _metrics;
    private readonly ILocalEventBus? _eventBus;
    private readonly ILogger<RequestRouter> _logger;
    private readonly IClock _clock;
    private readonly string _engineName;
    private readonly Guid _pageId;

    private readonly ConcurrentQueue<(RoutePattern Pattern, Func<RouteRequest, CancellationToken, ValueTask<RouteDecision>> Handler)> _handlers
        = new();

    public RequestRouter(
        IBrowserSandboxProfile sandbox,
        IUrlSafetyValidator urlSafety,
        BrowsingMetrics metrics,
        ILogger<RequestRouter> logger,
        IClock clock,
        string engineName,
        Guid pageId,
        ILocalEventBus? eventBus = null)
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
    }

    /// <inheritdoc/>
    public void Register(RoutePattern pattern, Func<RouteRequest, CancellationToken, ValueTask<RouteDecision>> handler)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        ArgumentNullException.ThrowIfNull(handler);
        _handlers.Enqueue((pattern, handler));
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

        // 5. User handlers (registration order).
        foreach ((RoutePattern pattern, Func<RouteRequest, CancellationToken, ValueTask<RouteDecision>> handler) in _handlers)
        {
            if (!pattern.IsMatch(request.Url))
            {
                continue;
            }

            try
            {
                RouteDecision decision = await handler(request, cancellationToken).ConfigureAwait(false);
                if (decision.Kind != RouteDecisionKind.Continue)
                {
                    return decision;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "User route handler threw; defaulting to Continue.");
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
        string candidate = $"{url.Host.ToLowerInvariant()}{url.AbsolutePath}";
        Matcher matcher = new();
        foreach (string pattern in patterns)
        {
            matcher.AddInclude(pattern.ToLowerInvariant());
        }

        return matcher.Match(candidate).HasMatches;
    }

    private async Task<RouteDecision> BlockAsync(RouteRequest request, string reason, CancellationToken cancellationToken)
    {
        _metrics.RecordError(_engineName, tenantId: null, $"sandbox.{reason}");

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
}
