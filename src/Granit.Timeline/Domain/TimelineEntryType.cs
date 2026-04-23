namespace Granit.Timeline.Domain;

/// <summary>
/// Discriminator for <see cref="TimelineEntry"/>.
/// Classifies entries as comment, system log, or internal note.
/// </summary>
public enum TimelineEntryType
{
    /// <summary>Human-authored comment visible to all followers. Soft-deletable (GDPR).</summary>
    Comment = 0,

    /// <summary>Auto-generated immutable system log. INSERT-only for ISO 27001 audit trail.</summary>
    SystemLog = 1,

    /// <summary>Human-authored internal note visible only to staff. Soft-deletable (GDPR).</summary>
    InternalNote = 2,
}
