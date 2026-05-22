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
            id, entryId, userId, "👍", now, "user-1", tenantId: null);

        reaction.Id.ShouldBe(id);
        reaction.EntryId.ShouldBe(entryId);
        reaction.UserId.ShouldBe(userId);
        reaction.Emoji.ShouldBe("👍");
        reaction.CreatedAt.ShouldBe(now);
        reaction.CreatedBy.ShouldBe("user-1");
    }

    [Fact]
    public void Create_with_invalid_emoji_throws()
    {
        ArgumentException ex = Should.Throw<ArgumentException>(() => Reaction.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "thumbs_up", DateTimeOffset.UtcNow, "user-1"));

        ex.Message.ShouldContain("thumbs_up");
        ex.Message.ShouldContain("Unicode");
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
            Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), "❤️", DateTimeOffset.UtcNow, "user-1"));

    [Fact]
    public void Create_with_empty_userId_throws() =>
        Should.Throw<ArgumentException>(() => Reaction.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, "❤️", DateTimeOffset.UtcNow, "user-1"));

    [Fact]
    public void Create_with_blank_createdBy_throws() =>
        Should.Throw<ArgumentException>(() => Reaction.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "❤️", DateTimeOffset.UtcNow, ""));

    [Fact]
    public void IMultiTenant_round_trips_explicitly()
    {
        var tenantId = Guid.NewGuid();
        var reaction = Reaction.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "❤️",
            DateTimeOffset.UtcNow, "user-1", tenantId);

        reaction.TenantId.ShouldBe(tenantId);
        ((IMultiTenant)reaction).TenantId.ShouldBe(tenantId);

        var newTenant = Guid.NewGuid();
        ((IMultiTenant)reaction).TenantId = newTenant;
        reaction.TenantId.ShouldBe(newTenant);
    }
}

public sealed class EmojiValidatorTests
{
    [Theory]
    [InlineData("👍")]                  // single base codepoint
    [InlineData("❤️")]                  // base + VS-16
    [InlineData("🎉")]
    [InlineData("🚀")]
    [InlineData("✅")]                  // BMP block 0x2600..0x27BF
    [InlineData("⚠️")]                  // BMP + VS-16
    [InlineData("👍🏽")]                 // base + Fitzpatrick modifier
    [InlineData("👨‍👩‍👧‍👦")]          // ZWJ family sequence
    [InlineData("1️⃣")]                 // keycap: digit + VS-16 + combining enclosing keycap
    public void IsValid_accepts_well_formed_unicode_emojis(string emoji) =>
        EmojiValidator.IsValid(emoji).ShouldBeTrue();

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("thumbs_up")]           // legacy short name no longer accepted
    [InlineData("<script>")]
    [InlineData("a")]
    [InlineData("👍 ")]                 // trailing space
    [InlineData("‍👍")]             // leading ZWJ
    [InlineData("👍‍")]             // trailing ZWJ
    [InlineData("👍‍‍👀")]    // doubled ZWJ
    public void IsValid_rejects_malformed_input(string emoji) =>
        EmojiValidator.IsValid(emoji).ShouldBeFalse();

    [Fact]
    public void IsValid_rejects_input_longer_than_max_length()
    {
        string oversize = new('a', EmojiValidator.MaxLength + 1);
        EmojiValidator.IsValid(oversize).ShouldBeFalse();
    }

    [Theory]
    [InlineData("👍", "👍")]            // no modifiers — unchanged
    [InlineData("👍🏽", "👍")]           // Fitzpatrick stripped
    [InlineData("👍🏿", "👍")]
    [InlineData("❤️", "❤")]            // VS-16 stripped
    [InlineData("👍🏽👀", "👍👀")]       // modifier in middle
    public void NormalizeForAggregate_collapses_skin_tone_and_vs16(string input, string expected) =>
        EmojiValidator.NormalizeForAggregate(input).ShouldBe(expected);

    [Fact]
    public void NormalizeForAggregate_returns_empty_for_null_or_empty()
    {
        EmojiValidator.NormalizeForAggregate(null).ShouldBe(string.Empty);
        EmojiValidator.NormalizeForAggregate("").ShouldBe(string.Empty);
    }
}
