using Granit.DataProtection;

namespace Granit.Timeline;

/// <summary>
/// A single entry in the unified activity stream DTO.
/// Discriminated by <see cref="EntryType"/>.
/// </summary>
public sealed record TimelineStreamEntry
{
    /// <summary>Unique identifier of this entry.</summary>
    public required Guid Id { get; init; }

    /// <summary>Timestamp for chronological ordering.</summary>
    public required DateTimeOffset OccurredAt { get; init; }

    /// <summary>Discriminator for the entry source.</summary>
    public required TimelineStreamEntryType EntryType { get; init; }

    /// <summary>Author user ID (null for system events without a specific user).</summary>
    public string? AuthorId { get; init; }

    /// <summary>Author display name.</summary>
    [SensitiveData]
    public string? AuthorName { get; init; }

    /// <summary>Content body (Markdown, structured text, or summary).</summary>
    public string Body { get; init; } = string.Empty;

    /// <summary>Attachments (empty for non-comment entries).</summary>
    public IReadOnlyList<TimelineAttachmentInfo> Attachments { get; init; } = [];

    /// <summary>Parent entry ID for threaded replies.</summary>
    public Guid? ParentEntryId { get; init; }

    /// <summary>
    /// Origin of this entry. <see cref="TimelineEntryOrigin.Native"/> for rows
    /// stored in the Timeline table directly; <see cref="TimelineEntryOrigin.External"/>
    /// for entries projected from an <see cref="Abstractions.ITimelineSource"/>.
    /// </summary>
    public TimelineEntryOrigin Origin { get; init; } = TimelineEntryOrigin.Native;

    /// <summary>
    /// Contributor key for <see cref="TimelineEntryOrigin.External"/> entries
    /// (e.g. <c>"auditing"</c>); <see cref="Abstractions.TimelineSourceKeys.Native"/>
    /// for native rows.
    /// </summary>
    public string SourceKey { get; init; } = Abstractions.TimelineSourceKeys.Native;

    /// <summary>
    /// External primary key in the contributor's store (string-encoded), or
    /// <see langword="null"/> for native rows.
    /// </summary>
    public string? SourceId { get; init; }

    /// <summary>
    /// Timestamp of the last body edit, or <see langword="null"/> if the entry
    /// has never been edited. Always <see langword="null"/> for
    /// <see cref="TimelineEntryOrigin.External"/> entries — those reflect
    /// upstream changes through a fresh projection, not this field.
    /// </summary>
    public DateTimeOffset? EditedAt { get; init; }
}
