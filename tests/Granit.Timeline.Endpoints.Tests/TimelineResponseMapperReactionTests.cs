using Granit.Timeline.Domain;
using Granit.Timeline.Endpoints.Dtos;
using Granit.Timeline.Endpoints.Internal;
using Shouldly;
using Xunit;

namespace Granit.Timeline.Endpoints.Tests;

public sealed class TimelineResponseMapperReactionTests
{
    [Fact]
    public void AggregateReactions_groups_per_entry_per_emoji_with_count()
    {
        var entry1 = Guid.NewGuid();
        var entry2 = Guid.NewGuid();
        var currentUser = Guid.NewGuid();
        var otherUser = Guid.NewGuid();

        Reaction[] reactions =
        [
            R(entry1, currentUser, "👍"),
            R(entry1, otherUser, "👍"),
            R(entry1, otherUser, "❤️"),
            R(entry2, otherUser, "🎉"),
        ];

        IReadOnlyDictionary<Guid, IReadOnlyDictionary<string, ReactionAggregateResponse>> result =
            TimelineResponseMapper.AggregateReactions(reactions, currentUser);

        result[entry1]["👍"].ShouldBe(new ReactionAggregateResponse(2, ByCurrentUser: true, DisplayEmoji: "👍"));
        // VS-16 stripped by NormalizeForAggregate — aggregate key is the bare heart codepoint
        // but DisplayEmoji preserves the original fully-qualified form (with VS-16).
        result[entry1]["❤"].ShouldBe(new ReactionAggregateResponse(1, ByCurrentUser: false, DisplayEmoji: "❤️"));
        result[entry2]["🎉"].ShouldBe(new ReactionAggregateResponse(1, ByCurrentUser: false, DisplayEmoji: "🎉"));
    }

    [Fact]
    public void AggregateReactions_with_no_current_user_marks_byCurrentUser_false()
    {
        var entryId = Guid.NewGuid();
        Reaction[] reactions = [R(entryId, Guid.NewGuid(), "❤️")];

        IReadOnlyDictionary<Guid, IReadOnlyDictionary<string, ReactionAggregateResponse>> result =
            TimelineResponseMapper.AggregateReactions(reactions, currentUserId: null);

        result[entryId]["❤"].ByCurrentUser.ShouldBeFalse();
    }

    [Fact]
    public void AggregateReactions_collapses_skin_tone_variants_under_base_codepoint()
    {
        var entryId = Guid.NewGuid();
        Reaction[] reactions =
        [
            R(entryId, Guid.NewGuid(), "👍"),
            R(entryId, Guid.NewGuid(), "👍🏽"),
            R(entryId, Guid.NewGuid(), "👍🏿"),
        ];

        IReadOnlyDictionary<Guid, IReadOnlyDictionary<string, ReactionAggregateResponse>> result =
            TimelineResponseMapper.AggregateReactions(reactions, currentUserId: null);

        result[entryId].Count.ShouldBe(1);
        result[entryId]["👍"].Count.ShouldBe(3);
    }

    [Fact]
    public void AggregateReactions_picks_a_fully_qualified_variant_as_DisplayEmoji()
    {
        // Two users react with skin-tone variants of the same base. DisplayEmoji
        // must be a variant (not the normalized/stripped form); first-seen wins.
        var entryId = Guid.NewGuid();
        Reaction[] reactions =
        [
            R(entryId, Guid.NewGuid(), "👍🏽"),
            R(entryId, Guid.NewGuid(), "👍🏿"),
        ];

        IReadOnlyDictionary<Guid, IReadOnlyDictionary<string, ReactionAggregateResponse>> result =
            TimelineResponseMapper.AggregateReactions(reactions, currentUserId: null);

        ReactionAggregateResponse aggregate = result[entryId]["👍"];
        aggregate.Count.ShouldBe(2);
        aggregate.DisplayEmoji.ShouldBe("👍🏽"); // first-seen variant
        // Sanity: any variant in the group is acceptable, but never the stripped key.
        aggregate.DisplayEmoji.ShouldBeOneOf("👍🏽", "👍🏿");
    }

    [Fact]
    public void AggregateReactions_preserves_VS16_in_ZWJ_sequence_as_DisplayEmoji()
    {
        // 👨‍⚕️ (man health worker) — canonical RGI form includes VS-16 inside
        // the ZWJ sequence. NormalizeForAggregate strips VS-16 from the key,
        // but DisplayEmoji must keep the original fully-qualified form so
        // Twemoji-style asset CDNs find their file (1f468-200d-2695-fe0f.svg).
        const string manHealthWorker = "👨‍⚕️";
        var entryId = Guid.NewGuid();
        Reaction[] reactions = [R(entryId, Guid.NewGuid(), manHealthWorker)];

        IReadOnlyDictionary<Guid, IReadOnlyDictionary<string, ReactionAggregateResponse>> result =
            TimelineResponseMapper.AggregateReactions(reactions, currentUserId: null);

        // Single aggregate; the key is the VS-16-stripped form.
        result[entryId].Count.ShouldBe(1);
        ReactionAggregateResponse aggregate = result[entryId].Values.Single();
        aggregate.DisplayEmoji.ShouldBe(manHealthWorker);
    }

    [Fact]
    public void AggregateReactions_with_empty_input_returns_empty_dict()
    {
        IReadOnlyDictionary<Guid, IReadOnlyDictionary<string, ReactionAggregateResponse>> result =
            TimelineResponseMapper.AggregateReactions([], currentUserId: Guid.NewGuid());

        result.ShouldBeEmpty();
    }

    private static Reaction R(Guid entryId, Guid userId, string emoji) =>
        Reaction.Create(
            id: Guid.NewGuid(),
            entryId: entryId,
            userId: userId,
            emoji: emoji,
            createdAt: DateTimeOffset.UtcNow,
            createdBy: userId.ToString());
}
