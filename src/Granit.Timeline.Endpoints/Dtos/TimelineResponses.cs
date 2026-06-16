namespace Granit.Timeline.Endpoints.Dtos;

/// <summary>
/// Activity stream entry. The <c>Reactions</c> field carries the per-emoji
/// reaction summary keyed by the base Unicode emoji codepoint (skin-tone
/// variants collapse under the base — see
/// <see cref="Granit.Timeline.Domain.EmojiValidator.NormalizeForAggregate"/>);
/// only emojis with at least one reaction are present and the field itself
/// is <see langword="null"/> when the entry has no reactions, keeping the
/// wire payload tight (story C3).
/// <para>
/// Federation provenance is surfaced verbatim from the projection:
/// <c>Origin</c> (<c>"Native"</c> for rows in the Timeline table, <c>"External"</c>
/// for entries projected from an <c>ITimelineSource</c>), <c>SourceKey</c>
/// (contributor key such as <c>"auditing"</c>; the reserved <c>"native"</c> for
/// native rows — never <see langword="null"/>), <c>SourceId</c> (external
/// primary key, <see langword="null"/> for native rows), and <c>EditedAt</c>
/// (last body-edit timestamp, <see langword="null"/> if never edited).
/// </para>
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
    TimelineEntryOrigin Origin,
    string SourceKey,
    string? SourceId,
    DateTimeOffset? EditedAt,
    IReadOnlyDictionary<string, ReactionAggregateResponse>? Reactions = null);

/// <summary>Attachment metadata.</summary>
public sealed record TimelineAttachmentInfoResponse(
    Guid Id,
    Guid BlobId,
    string FileName,
    string ContentType,
    long SizeBytes);
