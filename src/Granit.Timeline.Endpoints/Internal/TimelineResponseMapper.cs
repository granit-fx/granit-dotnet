using Granit.Timeline.Domain;
using Granit.Timeline.Endpoints.Dtos;

namespace Granit.Timeline.Endpoints.Internal;

internal static class TimelineResponseMapper
{
    internal static TimelineStreamEntryResponse ToResponse(
        TimelineStreamEntry entry,
        IReadOnlyDictionary<string, ReactionAggregateResponse>? reactions = null) =>
        new(entry.Id, entry.OccurredAt, entry.EntryType, entry.AuthorId, entry.AuthorName, entry.Body,
            [.. entry.Attachments.Select(ToResponse)], entry.ParentEntryId, reactions);

    internal static TimelineAttachmentInfoResponse ToResponse(TimelineAttachmentInfo attachment) =>
        new(attachment.Id, attachment.BlobId, attachment.FileName, attachment.ContentType, attachment.SizeBytes);

    /// <summary>
    /// Aggregates the flat reaction list returned by
    /// <c>IReactionReader.GetByEntriesAsync</c> into a per-entry,
    /// per-emoji summary suitable for the wire payload (story C3).
    /// Entries with no reactions are absent from the map (caller passes
    /// <see langword="null"/> for those).
    /// </summary>
    internal static IReadOnlyDictionary<Guid, IReadOnlyDictionary<string, ReactionAggregateResponse>> AggregateReactions(
        IReadOnlyList<Reaction> reactions,
        Guid? currentUserId)
    {
        Dictionary<Guid, Dictionary<string, (int Count, bool ByCurrentUser)>> byEntry = [];
        foreach (Reaction r in reactions)
        {
            if (!byEntry.TryGetValue(r.EntryId, out Dictionary<string, (int, bool)>? perEmoji))
            {
                perEmoji = new Dictionary<string, (int, bool)>(StringComparer.Ordinal);
                byEntry[r.EntryId] = perEmoji;
            }
            perEmoji.TryGetValue(r.Emoji, out (int Count, bool ByCurrentUser) cur);
            perEmoji[r.Emoji] = (cur.Count + 1, cur.ByCurrentUser || (currentUserId is { } uid && r.UserId == uid));
        }

        Dictionary<Guid, IReadOnlyDictionary<string, ReactionAggregateResponse>> result = [];
        foreach ((Guid entryId, Dictionary<string, (int Count, bool ByCurrentUser)> perEmoji) in byEntry)
        {
            Dictionary<string, ReactionAggregateResponse> summary = new(perEmoji.Count, StringComparer.Ordinal);
            foreach ((string emoji, (int Count, bool ByCurrentUser) agg) in perEmoji)
            {
                summary[emoji] = new ReactionAggregateResponse(agg.Count, agg.ByCurrentUser);
            }
            result[entryId] = summary;
        }
        return result;
    }
}
