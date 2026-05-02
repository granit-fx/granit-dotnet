namespace Granit.Activities;

/// <summary>
/// Immutable description of a kind of activity (ADR-046 §4) — the framework
/// ships a starter catalog (<see cref="StandardActivityTypes"/>); modules add
/// domain-specific types via <see cref="IActivityTypeProvider"/> grafts.
/// </summary>
/// <param name="Name">Stable wire identifier (e.g. <c>"ToDo"</c>, <c>"Quote"</c>). Unique across all loaded providers — duplicate names fail boot in the registry.</param>
/// <param name="Icon">Icon name from the framework's icon catalog (e.g. <c>"check-square"</c>, <c>"phone"</c>).</param>
/// <param name="DisplayKey">i18n key for the user-facing label (e.g. <c>"Activity:ToDo"</c>) — resolved by the runtime module's localization resource (story A2).</param>
/// <param name="DefaultDurationMinutes">Optional default duration in minutes for time-blocked types (Meeting, Call). When set, the cross-entity calendar (story A6) projects an end date as <c>DueAt + DefaultDurationMinutes</c>; otherwise the activity surfaces as a point-in-time entry.</param>
public sealed record ActivityType(
    string Name,
    string Icon,
    string DisplayKey,
    int? DefaultDurationMinutes = null);
