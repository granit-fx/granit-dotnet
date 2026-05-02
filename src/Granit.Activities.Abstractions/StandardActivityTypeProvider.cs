namespace Granit.Activities;

/// <summary>
/// <see cref="IActivityTypeProvider"/> exposing the framework's standard
/// catalog (<see cref="StandardActivityTypes.All"/>). Registered automatically
/// by the <c>Granit.Activities</c> runtime module (story A2); hosts that want
/// a strictly custom catalog can opt out by replacing this provider with their
/// own.
/// </summary>
public sealed class StandardActivityTypeProvider : IActivityTypeProvider
{
    /// <inheritdoc />
    public IEnumerable<ActivityType> Provide() => StandardActivityTypes.All;
}
