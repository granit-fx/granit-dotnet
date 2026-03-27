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
}
