namespace Granit.Timeline.Endpoints.Dtos;

/// <summary>
/// Activity stream entry. The <c>Reactions</c> field carries the per-emoji
/// reaction summary keyed by the base Unicode emoji codepoint (skin-tone
/// variants collapse under the base — see
/// <see cref="Granit.Timeline.Domain.EmojiValidator.NormalizeForAggregate"/>);
/// only emojis with at least one reaction are present and the field itself
/// is <see langword="null"/> when the entry has no reactions, keeping the
/// wire payload tight (story C3).
/// </summary>
public sealed record TimelineStreamEntryResponse(
    Guid Id,
    DateTimeOffset OccurredAt,
    TimelineStreamEntryType EntryType,
    string? AuthorId,
    string? AuthorName,
    string Body,
    IReadOnlyList<TimelineAttachmentInfoResponse> Attachments,
    Guid? ParentEntryId,
    IReadOnlyDictionary<string, ReactionAggregateResponse>? Reactions = null);

/// <summary>Attachment metadata.</summary>
public sealed record TimelineAttachmentInfoResponse(
    Guid Id,
    Guid BlobId,
    string FileName,
    string ContentType,
    long SizeBytes);
