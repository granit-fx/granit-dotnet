using System.Globalization;

namespace Granit.Analytics.Endpoints.Internal;

/// <summary>
/// Composes a deterministic FusionCache key for a metric request. The tenant id is
/// always included — without it, a cached value could be served cross-tenant. This is
/// the central security boundary of the cache layer.
/// </summary>
internal static class MetricCacheKey
{
    /// <summary>
    /// <c>analytics:metric:{name}:{tenantId|global}:{period.from}:{period.to}[:cmp:{compareTo.from}:{compareTo.to}]</c>.
    /// </summary>
    public static string Compose(
        string metricName,
        string? tenantId,
        ResolvedPeriod? period,
        ResolvedPeriod? compareTo)
    {
        string baseKey = period is { } p
            ? $"analytics:metric:{metricName}:{tenantId ?? "global"}:{Format(p.From)}:{Format(p.To)}"
            : $"analytics:metric:{metricName}:{tenantId ?? "global"}:no-period";

        return compareTo is { } c
            ? $"{baseKey}:cmp:{Format(c.From)}:{Format(c.To)}"
            : baseKey;
    }

    private static string Format(DateTimeOffset value) =>
        value.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
}
