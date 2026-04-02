using System.Diagnostics;

namespace Granit.Auditing.Diagnostics;

/// <summary>
/// Central <see cref="ActivitySource"/> for Granit.Auditing distributed tracing.
/// </summary>
/// <remarks>
/// <c>Granit.Observability</c> adds this source automatically via
/// <c>GranitActivitySourceRegistry</c> when both packages are used.
/// </remarks>
internal static class AuditingActivitySource
{
    /// <summary>The name of the Granit.Auditing <see cref="ActivitySource"/>.</summary>
    internal const string Name = "Granit.Auditing";

    /// <summary>The singleton <see cref="ActivitySource"/> instance.</summary>
    internal static readonly ActivitySource Source = new(Name);

    // ──── Operation names ────

    internal const string Capture = "auditing.capture";
    internal const string Persist = "auditing.persist";
    internal const string Cleanup = "auditing.cleanup";
    internal const string Pseudonymize = "auditing.pseudonymize";
}
