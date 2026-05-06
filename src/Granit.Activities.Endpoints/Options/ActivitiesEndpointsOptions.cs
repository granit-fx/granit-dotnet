namespace Granit.Activities.Endpoints.Options;

/// <summary>
/// Configuration options for the activities endpoints.
/// </summary>
public sealed class ActivitiesEndpointsOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "ActivitiesEndpoints";

    /// <summary>Route prefix for all activity endpoints. Default: <c>"activities"</c>.</summary>
    public string RoutePrefix { get; set; } = "activities";

    /// <summary>OpenAPI tag name for grouping endpoints in Scalar / Swagger UI. Default: <c>"Activities"</c>.</summary>
    public string TagName { get; set; } = "Activities";

    /// <summary>Maximum page size accepted by the list endpoint. Default: <c>100</c>.</summary>
    public int MaxPageSize { get; set; } = 100;

    /// <summary>Default page size when the caller does not specify one. Default: <c>20</c>.</summary>
    public int DefaultPageSize { get; set; } = 20;

    /// <summary>Maximum width of the cross-entity calendar window in days (story #1801). Default: <c>90</c>.</summary>
    public int MaxCalendarRangeDays { get; set; } = 90;

    /// <summary>FusionCache TTL for the activities calendar response. Default: 1 minute (sliding).</summary>
    public TimeSpan CalendarCacheTtl { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Upper bound (in days from now) accepted by Create/Reschedule for
    /// <see cref="Granit.Activities.Domain.Activity.DueAt"/>. Closes VULN-203 —
    /// without a bound, callers can mass-mute their inbox by rescheduling every
    /// open activity to <c>DateTimeOffset.MaxValue</c> (rows then never match
    /// the overdue scan and remain perpetually <c>Open</c>). Default: 5 years.
    /// </summary>
    public int MaxFutureRescheduleDays { get; set; } = 365 * 5;

    /// <summary>
    /// Window-snap granularity (minutes) applied to the calendar
    /// <c>from</c>/<c>to</c> parameters before cache-key composition. Closes
    /// VULN-204 — without snapping, an attacker can shift the window by one
    /// second per call to defeat <see cref="CalendarCacheTtl"/> and force
    /// repeated DB scans. Default: 60 minutes (one cache slot per hour).
    /// </summary>
    public int CalendarWindowSnapMinutes { get; set; } = 60;
}
