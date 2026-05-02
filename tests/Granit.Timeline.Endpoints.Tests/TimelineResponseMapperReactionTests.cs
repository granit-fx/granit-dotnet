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
            R(entry1, currentUser, "thumbs_up"),
            R(entry1, otherUser, "thumbs_up"),
            R(entry1, otherUser, "heart"),
            R(entry2, otherUser, "tada"),
        ];

        IReadOnlyDictionary<Guid, IReadOnlyDictionary<string, ReactionAggregateResponse>> result =
            TimelineResponseMapper.AggregateReactions(reactions, currentUser);

        result[entry1]["thumbs_up"].ShouldBe(new ReactionAggregateResponse(2, ByCurrentUser: true));
        result[entry1]["heart"].ShouldBe(new ReactionAggregateResponse(1, ByCurrentUser: false));
        result[entry2]["tada"].ShouldBe(new ReactionAggregateResponse(1, ByCurrentUser: false));
    }

    [Fact]
    public void AggregateReactions_with_no_current_user_marks_byCurrentUser_false()
    {
        var entryId = Guid.NewGuid();
        Reaction[] reactions = [R(entryId, Guid.NewGuid(), "heart")];

        IReadOnlyDictionary<Guid, IReadOnlyDictionary<string, ReactionAggregateResponse>> result =
            TimelineResponseMapper.AggregateReactions(reactions, currentUserId: null);

        result[entryId]["heart"].ByCurrentUser.ShouldBeFalse();
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
