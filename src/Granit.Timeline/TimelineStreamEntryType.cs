namespace Granit.Timeline;

/// <summary>
/// Discriminator for <see cref="TimelineStreamEntry"/> in the unified activity stream.
/// Extends <see cref="Domain.TimelineEntryType"/> with future sources.
/// </summary>
/// <remarks>
/// Numeric values MUST stay in lock-step with <see cref="Domain.TimelineEntryType"/> so that the
/// domain-to-wire projection is a direct cast — a divergence silently swaps InternalNote and
/// SystemLog on the wire (a staff-only note then leaks as a system log to followers).
/// </remarks>
public enum TimelineStreamEntryType
{
    /// <summary>Human-authored comment.</summary>
    Comment,

    /// <summary>Auto-generated system log entry.</summary>
    SystemLog,

    /// <summary>Human-authored internal note (staff-only).</summary>
    InternalNote,
}
