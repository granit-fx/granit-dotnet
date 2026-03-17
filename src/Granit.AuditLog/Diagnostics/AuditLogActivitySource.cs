using System.Diagnostics;

namespace Granit.AuditLog.Diagnostics;

/// <summary>
/// Central <see cref="ActivitySource"/> for Granit.AuditLog distributed tracing.
/// </summary>
/// <remarks>
/// <c>Granit.Observability</c> adds this source automatically via
/// <c>GranitActivitySourceRegistry</c> when both packages are used.
/// </remarks>
internal static class AuditLogActivitySource
{
    /// <summary>The name of the Granit.AuditLog <see cref="ActivitySource"/>.</summary>
    internal const string Name = "Granit.AuditLog";

    /// <summary>The singleton <see cref="ActivitySource"/> instance.</summary>
    internal static readonly ActivitySource Source = new(Name);

    // ──── Operation names ────

    internal const string Capture = "auditlog.capture";
    internal const string Persist = "auditlog.persist";
    internal const string Cleanup = "auditlog.cleanup";
}
