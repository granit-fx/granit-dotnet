namespace Granit.Timeline;

/// <summary>
/// Where a <see cref="TimelineStreamEntry"/> originates in the federated stream.
/// </summary>
/// <remarks>
/// The concrete source name (<c>"auditing"</c>, <c>"workflow"</c>, …) lives in
/// <see cref="TimelineStreamEntry.SourceKey"/> as a soft string contract, so new
/// contributors can plug in without bumping this enum.
/// </remarks>
public enum TimelineEntryOrigin
{
    /// <summary>Entry stored directly in the Timeline table (Comment, InternalNote, or native SystemLog).</summary>
    Native = 0,

    /// <summary>Entry projected from a registered <see cref="Abstractions.ITimelineSource"/>.</summary>
    External = 1,
}
