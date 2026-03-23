using System.Diagnostics;

namespace Granit.RateLimiting.Diagnostics;

/// <summary>
/// Central <see cref="ActivitySource"/> for Granit.RateLimiting distributed tracing.
/// </summary>
/// <remarks>
/// <c>Granit.Observability</c> adds this source automatically via
/// <c>GranitActivitySourceRegistry</c> when both packages are used.
/// </remarks>
internal static class RateLimitingActivitySource
{
    /// <summary>The name of the Granit.RateLimiting <see cref="ActivitySource"/>.</summary>
    internal const string Name = "Granit.RateLimiting";

    /// <summary>The singleton <see cref="ActivitySource"/> instance.</summary>
    internal static readonly ActivitySource Source = new(Name);

    // ──── Operation names ────

    internal const string Check = "ratelimiting.check";
}
