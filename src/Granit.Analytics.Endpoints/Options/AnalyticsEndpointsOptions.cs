using System.ComponentModel.DataAnnotations;

namespace Granit.Analytics.Endpoints.Options;

/// <summary>
/// Configuration options for the Granit.Analytics endpoint surface. Bound from the
/// <c>AnalyticsEndpoints</c> section of <c>appsettings.json</c> by
/// <c>AddGranitAnalyticsEndpoints</c>; data-annotation constraints are validated on
/// startup via <c>ValidateDataAnnotations</c> + <c>ValidateOnStart</c> AND through
/// <see cref="IValidatableObject"/> for the <see cref="TimeSpan"/> ranges that
/// <c>RangeAttribute</c> cannot express.
/// </summary>
public sealed class AnalyticsEndpointsOptions : IValidatableObject
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "AnalyticsEndpoints";

    /// <summary>Lower bound (inclusive) for the cache TTL options — values below this disable caching entirely instead.</summary>
    private static readonly TimeSpan MinTtl = TimeSpan.FromSeconds(1);

    /// <summary>Upper bound (inclusive) for <see cref="DynamicTtl"/>: 1 day. A dynamic metric stale for longer is no longer dynamic.</summary>
    private static readonly TimeSpan MaxDynamicTtl = TimeSpan.FromDays(1);

    /// <summary>Upper bound (inclusive) for <see cref="StaticTtl"/>: 7 days. Beyond a week, restart the host and re-warm.</summary>
    private static readonly TimeSpan MaxStaticTtl = TimeSpan.FromDays(7);

    /// <summary>Upper bound (inclusive) for <see cref="MaxPeriodLength"/>: 50 years.</summary>
    private static readonly TimeSpan MaxPeriodLengthCeiling = TimeSpan.FromDays(365 * 50);

    /// <summary>
    /// Route prefix for analytics endpoints. Default: <c>"analytics"</c>
    /// → final route <c>/analytics/metrics/{name}</c>.
    /// </summary>
    [Required]
    [MinLength(1)]
    public string RoutePrefix { get; set; } = "analytics";

    /// <summary>
    /// OpenAPI tag for metric endpoints. Default: <c>"Analytics"</c>.
    /// Per CLAUDE.md, modules with a single tag use the module's user-facing name directly;
    /// promote to a sub-tag (<c>"Analytics - Metrics"</c>) only once a second sibling tag exists.
    /// </summary>
    [Required]
    [MinLength(1)]
    public string MetricsTagName { get; set; } = "Analytics";

    /// <summary>
    /// FusionCache TTL applied to <see cref="Metrics.RefreshHint.Dynamic"/> metrics.
    /// Default: 60 seconds. Static metrics use <see cref="StaticTtl"/>; realtime metrics
    /// bypass the cache entirely — they are routed through the framework push transport
    /// (<c>Granit.Dashboards.Push</c>, ADR-043) when the host has loaded it, and
    /// degrade to <see cref="DynamicTtl"/> when it isn't loaded.
    /// </summary>
    public TimeSpan DynamicTtl { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// FusionCache TTL applied to <see cref="Metrics.RefreshHint.Static"/> metrics.
    /// Default: 5 minutes.
    /// </summary>
    public TimeSpan StaticTtl { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Maximum length accepted for an absolute <c>{from, to}</c> period and for the
    /// implicit comparison window resolved from <c>previous_period</c>. Default: 5 years.
    /// Caps unbounded aggregate scans an authenticated caller could otherwise force by
    /// submitting <c>{ from: 0001-01-01, to: 9999-12-31 }</c>.
    /// </summary>
    public TimeSpan MaxPeriodLength { get; set; } = TimeSpan.FromDays(365 * 5);

    /// <inheritdoc />
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (DynamicTtl < MinTtl || DynamicTtl > MaxDynamicTtl)
        {
            yield return new ValidationResult(
                $"{nameof(DynamicTtl)} must be between {MinTtl} and {MaxDynamicTtl} (current: {DynamicTtl}).",
                [nameof(DynamicTtl)]);
        }

        if (StaticTtl < MinTtl || StaticTtl > MaxStaticTtl)
        {
            yield return new ValidationResult(
                $"{nameof(StaticTtl)} must be between {MinTtl} and {MaxStaticTtl} (current: {StaticTtl}).",
                [nameof(StaticTtl)]);
        }

        if (MaxPeriodLength <= TimeSpan.Zero || MaxPeriodLength > MaxPeriodLengthCeiling)
        {
            yield return new ValidationResult(
                $"{nameof(MaxPeriodLength)} must be strictly positive and at most {MaxPeriodLengthCeiling} (current: {MaxPeriodLength}).",
                [nameof(MaxPeriodLength)]);
        }
    }
}
