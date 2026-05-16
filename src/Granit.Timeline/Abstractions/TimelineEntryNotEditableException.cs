namespace Granit.Timeline.Abstractions;

/// <summary>
/// Thrown by <see cref="ITimelineWriter.UpdateEntryBodyAsync"/> when one of the
/// four edit gates rejects the request. The endpoint maps this exception to a
/// <c>403 timeline-entry-not-editable</c> RFC 7807 response and surfaces
/// <see cref="Reason"/> in <c>extensions["reason"]</c> for the front-end.
/// </summary>
public sealed class TimelineEntryNotEditableException(TimelineEntryNotEditableReason reason, string message)
    : InvalidOperationException(message)
{
    /// <summary>Machine-readable rejection reason.</summary>
    public TimelineEntryNotEditableReason Reason { get; } = reason;
}

/// <summary>Why a timeline entry edit was rejected by the writer.</summary>
public enum TimelineEntryNotEditableReason
{
    /// <summary>The entry is projected from an external <see cref="ITimelineSource"/> (anchor shadow or live projection).</summary>
    ExternalOrigin = 0,

    /// <summary>The entry is a <c>SystemLog</c> — immutable by design (ISO 27001).</summary>
    SystemLog = 1,

    /// <summary>The current user is not the author of the entry.</summary>
    NotAuthor = 2,

    /// <summary>The edit window configured in <c>TimelineOptions.EditWindow</c> has elapsed.</summary>
    WindowExpired = 3,
}
