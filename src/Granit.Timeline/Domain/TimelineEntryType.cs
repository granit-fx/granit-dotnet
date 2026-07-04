namespace Granit.Timeline.Domain;

/// <summary>
/// Discriminator for <see cref="TimelineEntry"/>.
/// Classifies entries as comment, system log, or internal note.
/// </summary>
public enum TimelineEntryType
{
    /// <summary>Human-authored comment visible to all followers. Soft-deletable (GDPR).</summary>
    Comment,

    /// <summary>Auto-generated immutable system log. INSERT-only for ISO 27001 audit trail.</summary>
    SystemLog,

    /// <summary>Human-authored internal note visible only to staff. Soft-deletable (GDPR).</summary>
    InternalNote,
}
