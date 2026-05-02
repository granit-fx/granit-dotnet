using Granit.Activities.Abstractions;

namespace Granit.Activities.Endpoints.Dtos;

/// <summary>
/// Wire shape for the cross-entity activities calendar query parameters
/// (<c>GET /api/activities/calendar</c>). Bound from the query string by
/// ASP.NET Core's model binder.
/// </summary>
/// <param name="From">Inclusive start of the requested window.</param>
/// <param name="To">Exclusive end of the requested window — half-open. Window must be &lt;= <c>ActivitiesEndpointsOptions.MaxCalendarRangeDays</c>.</param>
/// <param name="Assignee">Optional assignee filter. Accepts <c>"me"</c> (resolved from <see cref="System.Security.Claims.ClaimsPrincipal"/>) or a string-form <see cref="System.Guid"/>.</param>
/// <param name="EntityType">Optional polymorphic FK filter — only activities for this host entity wire identifier (e.g. <c>"Granit.Parties.Party"</c>).</param>
/// <param name="Type">Optional comma-separated activity type names (e.g. <c>"Call,Meeting"</c>).</param>
/// <param name="Status">Optional status filter. Defaults to <c>OpenOrOverdue</c> when omitted.</param>
public sealed record ActivityCalendarRequest(
    DateTimeOffset From,
    DateTimeOffset To,
    string? Assignee = null,
    string? EntityType = null,
    string? Type = null,
    ActivityStatusFilter? Status = null);
