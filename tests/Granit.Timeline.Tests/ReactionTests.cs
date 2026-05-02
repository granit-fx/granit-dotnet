using Granit.Domain;
using Granit.Timeline.Domain;
using Shouldly;
using Xunit;

namespace Granit.Timeline.Tests;

public sealed class ReactionTests
{
    [Fact]
    public void Create_with_valid_emoji_succeeds()
    {
        var id = Guid.NewGuid();
        var entryId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        DateTimeOffset now = new(2026, 5, 2, 12, 0, 0, TimeSpan.Zero);

        var reaction = Reaction.Create(
            id, entryId, userId, "thumbs_up", now, "user-1", tenantId: null);

        reaction.Id.ShouldBe(id);
        reaction.EntryId.ShouldBe(entryId);
        reaction.UserId.ShouldBe(userId);
        reaction.Emoji.ShouldBe("thumbs_up");
        reaction.CreatedAt.ShouldBe(now);
        reaction.CreatedBy.ShouldBe("user-1");
    }

    [Fact]
    public void Create_with_unknown_emoji_throws()
    {
        ArgumentException ex = Should.Throw<ArgumentException>(() => Reaction.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "rocket", DateTimeOffset.UtcNow, "user-1"));

        ex.Message.ShouldContain("rocket");
        ex.Message.ShouldContain("catalog");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Create_with_blank_emoji_throws(string emoji) =>
        Should.Throw<ArgumentException>(() => Reaction.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), emoji, DateTimeOffset.UtcNow, "user-1"));

    [Fact]
    public void Create_with_empty_entryId_throws() =>
        Should.Throw<ArgumentException>(() => Reaction.Create(
            Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), "heart", DateTimeOffset.UtcNow, "user-1"));

    [Fact]
    public void Create_with_empty_userId_throws() =>
        Should.Throw<ArgumentException>(() => Reaction.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, "heart", DateTimeOffset.UtcNow, "user-1"));

    [Fact]
    public void Create_with_blank_createdBy_throws() =>
        Should.Throw<ArgumentException>(() => Reaction.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "heart", DateTimeOffset.UtcNow, ""));

    [Fact]
    public void IMultiTenant_round_trips_explicitly()
    {
        var tenantId = Guid.NewGuid();
        var reaction = Reaction.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "heart",
            DateTimeOffset.UtcNow, "user-1", tenantId);

        reaction.TenantId.ShouldBe(tenantId);
        ((IMultiTenant)reaction).TenantId.ShouldBe(tenantId);

        var newTenant = Guid.NewGuid();
        ((IMultiTenant)reaction).TenantId = newTenant;
        reaction.TenantId.ShouldBe(newTenant);
    }
}

public sealed class ReactionEmojiCatalogTests
{
    [Fact]
    public void All_returns_5_v1_emojis()
    {
        ReactionEmojiCatalog.All.Count.ShouldBe(5);
        ReactionEmojiCatalog.All.ShouldBe(
            ["thumbs_up", "heart", "tada", "joy", "eyes"]);
    }

    [Theory]
    [InlineData("thumbs_up", true)]
    [InlineData("heart", true)]
    [InlineData("tada", true)]
    [InlineData("joy", true)]
    [InlineData("eyes", true)]
    [InlineData("rocket", false)]
    [InlineData("THUMBS_UP", false)] // case-sensitive (Ordinal)
    [InlineData("", false)]
    public void IsValid_recognises_only_catalog_keys(string emoji, bool expected) =>
        ReactionEmojiCatalog.IsValid(emoji).ShouldBe(expected);
}
