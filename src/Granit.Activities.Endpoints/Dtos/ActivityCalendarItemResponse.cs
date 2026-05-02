using Granit.Activities.Domain;

namespace Granit.Activities.Endpoints.Dtos;

/// <summary>
/// One activity positioned on the cross-entity calendar's time axis. The
/// React shell projects this shape into its `<Calendar />` component and
/// composes the displayed title client-side from the DisplayKey on each
/// <see cref="Granit.Activities.ActivityType"/>.
/// </summary>
/// <param name="Id">Stable identifier of the activity row — drives the detail-page link.</param>
/// <param name="Start">Activity due-date.</param>
/// <param name="End">Activity end (<c>DueAt + DefaultDurationMinutes</c> when the type carries a duration); <see langword="null"/> renders a point-in-time marker.</param>
/// <param name="Title">Server-side composed display label — built from the activity type name + entity reference. Frontend may override using the i18n key it receives via the <c>activities</c> manifest section.</param>
/// <param name="Color">Stable colour bucket derived from <see cref="Status"/> — <c>"open"</c>, <c>"overdue"</c>, <c>"done"</c>, <c>"cancelled"</c>. The renderer maps these to theme colours.</param>
/// <param name="Type">Activity type name (raw, e.g. <c>"Call"</c>) — pairs with the manifest catalog's i18n key on the frontend.</param>
/// <param name="Status">Lifecycle status of the activity — see <see cref="ActivityStatus"/>.</param>
/// <param name="EntityType">Polymorphic FK target — host entity wire identifier.</param>
/// <param name="EntityId">Polymorphic FK target — host entity row id.</param>
/// <param name="AssignedToUserId">User to whom the activity is assigned.</param>
public sealed record ActivityCalendarItemResponse(
    Guid Id,
    DateTimeOffset Start,
    DateTimeOffset? End,
    string Title,
    string Color,
    string Type,
    ActivityStatus Status,
    string EntityType,
    Guid EntityId,
    Guid AssignedToUserId);
