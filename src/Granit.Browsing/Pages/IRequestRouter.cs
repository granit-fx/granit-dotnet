using System;
using System.Threading;
using System.Threading.Tasks;

namespace Granit.Browsing.Pages;

/// <summary>
/// Provider-neutral request-policy chain. A single instance owns interception for a
/// page; sandbox rules always evaluate before user-registered handlers (sandbox wins).
/// </summary>
/// <remarks>
/// <para>
/// Closes VULN-101 (provider-specific race between several <c>page.Request +=</c>
/// subscribers) by funnelling every request through one router.
/// </para>
/// <para>
/// Closes VULN-001 (SSRF at request time, defeating DNS rebinding) by re-validating the
/// host via <c>Granit.Http.Security.IUrlSafetyValidator</c> on every intercepted request.
/// </para>
/// </remarks>
internal interface IRequestRouter
{
    /// <summary>Evaluates <paramref name="request"/> against sandbox rules and user handlers.</summary>
    ValueTask<RouteDecision> EvaluateAsync(RouteRequest request, CancellationToken cancellationToken);

    /// <summary>Registers a user-supplied handler for requests matching <paramref name="pattern"/>.</summary>
    void Register(RoutePattern pattern, Func<RouteRequest, CancellationToken, ValueTask<RouteDecision>> handler);
}
