namespace Granit.Browsing.Pages;

/// <summary>
/// Provider-neutral request-policy chain. A single instance owns interception for a
/// page; sandbox rules always evaluate before user-registered handlers (sandbox wins).
/// </summary>
/// <remarks>
/// <para>
/// Eliminates the provider-specific race between several <c>page.Request +=</c>
/// subscribers by funnelling every request through one router.
/// </para>
/// <para>
/// Re-validates the host via <c>Granit.Http.UrlSafety.IUrlSafetyValidator</c> on every
/// intercepted request, defeating DNS-rebinding SSRF between navigation and fetch time.
/// </para>
/// </remarks>
internal interface IRequestRouter
{
    /// <summary>Evaluates <paramref name="request"/> against sandbox rules and user handlers.</summary>
    ValueTask<RouteDecision> EvaluateAsync(RouteRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Registers a user-supplied handler for requests matching <paramref name="pattern"/>.
    /// Disposing the returned token removes the handler — keep the token alive for the
    /// lifetime of the subscription.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the per-page registration cap has been reached.
    /// </exception>
    IDisposable Register(RoutePattern pattern, Func<RouteRequest, CancellationToken, ValueTask<RouteDecision>> handler);
}
