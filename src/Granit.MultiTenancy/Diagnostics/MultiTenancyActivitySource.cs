using System.Diagnostics;

namespace Granit.MultiTenancy.Diagnostics;

/// <summary>
/// Central <see cref="ActivitySource"/> for Granit.MultiTenancy distributed tracing.
/// </summary>
/// <remarks>
/// <c>Granit.Observability</c> adds this source automatically via
/// <c>GranitActivitySourceRegistry</c> when both packages are used. Spans are only
/// materialized when a listener is attached — <see cref="ActivitySource.StartActivity(string, ActivityKind)"/>
/// returns <c>null</c> otherwise, so instrumentation is allocation-free at rest.
/// </remarks>
internal static class MultiTenancyActivitySource
{
    /// <summary>The name of the Granit.MultiTenancy <see cref="ActivitySource"/>.</summary>
    internal const string Name = "Granit.MultiTenancy";

    /// <summary>The singleton <see cref="ActivitySource"/> instance.</summary>
    internal static readonly ActivitySource Source = new(Name);

    // ──── Operation names ────

    /// <summary>Per-request tenant resolution in <c>TenantResolutionMiddleware</c>.</summary>
    internal const string Resolve = "multitenancy.resolve";
}
