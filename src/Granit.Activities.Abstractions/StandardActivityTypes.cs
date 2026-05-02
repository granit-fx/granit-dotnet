namespace Granit.Activities;

/// <summary>
/// The framework's starter catalog of activity types (ADR-046 §4). Registered
/// automatically by the <c>Granit.Activities</c> runtime module (story A2) via
/// <see cref="StandardActivityTypeProvider"/>; entities can opt out with
/// <c>AllowedTypes(...)</c> on their <c>EntityDefinition</c> if they only
/// support a subset.
/// </summary>
public static class StandardActivityTypes
{
    /// <summary>Generic to-do — single point in time, no default duration.</summary>
    public static readonly ActivityType ToDo = new(
        Name: "ToDo",
        Icon: "check-square",
        DisplayKey: "Activity:ToDo");

    /// <summary>Phone call — single point in time, no default duration.</summary>
    public static readonly ActivityType Call = new(
        Name: "Call",
        Icon: "phone",
        DisplayKey: "Activity:Call");

    /// <summary>Meeting — time-blocked, default 30-minute duration.</summary>
    public static readonly ActivityType Meeting = new(
        Name: "Meeting",
        Icon: "calendar",
        DisplayKey: "Activity:Meeting",
        DefaultDurationMinutes: 30);

    /// <summary>Email follow-up — single point in time, no default duration.</summary>
    public static readonly ActivityType Email = new(
        Name: "Email",
        Icon: "mail",
        DisplayKey: "Activity:Email");

    /// <summary>The four standard types in declaration order.</summary>
    public static IReadOnlyList<ActivityType> All { get; } = [ToDo, Call, Meeting, Email];
}
