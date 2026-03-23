using Granit.Timeline.Domain;

namespace Granit.Timeline.Endpoints.Dtos;

/// <summary>
/// Request DTO for posting a new timeline entry.
/// </summary>
public sealed record PostTimelineEntryRequest
{
    /// <summary>Type of entry to create (Comment or InternalNote only; SystemLog is system-only).</summary>
    public required TimelineEntryType EntryType { get; init; }

    /// <summary>Markdown body of the entry.</summary>
    public required string Body { get; init; }

    /// <summary>Optional parent entry ID for threaded replies.</summary>
    public Guid? ParentEntryId { get; init; }

    /// <summary>Optional blob IDs to attach to the entry.</summary>
    public IReadOnlyList<Guid>? AttachmentBlobIds { get; init; }
}
