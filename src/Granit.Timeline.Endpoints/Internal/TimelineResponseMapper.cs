using Granit.Timeline.Domain;
using Granit.Timeline.Endpoints.Dtos;

namespace Granit.Timeline.Endpoints.Internal;

internal static class TimelineResponseMapper
{
    internal static TimelineStreamEntryResponse ToResponse(
        TimelineStreamEntry entry,
        IReadOnlyDictionary<string, ReactionAggregateResponse>? reactions = null) =>
        new(entry.Id, entry.OccurredAt, entry.EntryType, entry.AuthorId, entry.AuthorName, entry.Body,
            [.. entry.Attachments.Select(ToResponse)], entry.ParentEntryId,
            entry.Origin, entry.SourceKey, entry.SourceId, entry.EditedAt, reactions);

    internal static TimelineAttachmentInfoResponse ToResponse(TimelineAttachmentInfo attachment) =>
        new(attachment.Id, attachment.BlobId, attachment.FileName, attachment.ContentType, attachment.SizeBytes);

    /// <summary>
    /// Aggregates the flat reaction list returned by
    /// <c>IReactionReader.GetByEntriesAsync</c> into a per-entry,
    /// per-emoji summary suitable for the wire payload (story C3).
    /// Skin-tone variants and VS-16 selectors are collapsed via
    /// <see cref="EmojiValidator.NormalizeForAggregate"/> so 👍 / 👍🏽 /
    /// 👍🏿 share a single counter keyed under the base codepoint. The
    /// first reaction encountered in each group wins as the
    /// <see cref="ReactionAggregateResponse.DisplayEmoji"/> (the reader
    /// orders rows by <c>CreatedAt</c>, so this is the oldest reaction
    /// — deterministic across calls). Entries with no reactions are
    /// absent from the map (caller passes <see langword="null"/> for
    /// those).
    /// </summary>
    internal static IReadOnlyDictionary<Guid, IReadOnlyDictionary<string, ReactionAggregateResponse>> AggregateReactions(
        IReadOnlyList<Reaction> reactions,
        Guid? currentUserId)
    {
        Dictionary<Guid, Dictionary<string, (int Count, bool ByCurrentUser, string DisplayEmoji)>> byEntry = [];
        foreach (Reaction r in reactions)
        {
            if (!byEntry.TryGetValue(r.EntryId, out Dictionary<string, (int, bool, string)>? perEmoji))
            {
                perEmoji = new Dictionary<string, (int, bool, string)>(StringComparer.Ordinal);
                byEntry[r.EntryId] = perEmoji;
            }
            string key = EmojiValidator.NormalizeForAggregate(r.Emoji);
            bool byCurrentUser = currentUserId is { } uid && r.UserId == uid;
            if (perEmoji.TryGetValue(key, out (int Count, bool ByCurrentUser, string DisplayEmoji) cur))
            {
                perEmoji[key] = (cur.Count + 1, cur.ByCurrentUser || byCurrentUser, cur.DisplayEmoji);
            }
            else
            {
                perEmoji[key] = (1, byCurrentUser, r.Emoji);
            }
        }

        Dictionary<Guid, IReadOnlyDictionary<string, ReactionAggregateResponse>> result = [];
        foreach ((Guid entryId, Dictionary<string, (int Count, bool ByCurrentUser, string DisplayEmoji)> perEmoji) in byEntry)
        {
            Dictionary<string, ReactionAggregateResponse> summary = new(perEmoji.Count, StringComparer.Ordinal);
            foreach ((string emoji, (int Count, bool ByCurrentUser, string DisplayEmoji) agg) in perEmoji)
            {
                summary[emoji] = new ReactionAggregateResponse(agg.Count, agg.ByCurrentUser, agg.DisplayEmoji);
            }
            result[entryId] = summary;
        }
        return result;
    }
}
