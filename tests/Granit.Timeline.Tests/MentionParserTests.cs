// =============================================================================
// Tests — MentionParser
// =============================================================================
// Verifies extraction of @[Name](user:guid) mentions from Markdown body.
// =============================================================================

using Granit.Timeline.Internal;
using Shouldly;
using Xunit;

namespace Granit.Timeline.Tests;

public sealed class MentionParserTests
{
    [Fact]
    public void ExtractMentionedUserIds_WithValidMention_ReturnsUserId()
    {
        const string body = "Hello @[Dr. Martin](user:550e8400-e29b-41d4-a716-446655440000), please review.";

        IReadOnlyList<string> result = MentionParser.ExtractMentionedUserIds(body);

        result.Count.ShouldBe(1);
        result[0].ShouldBe("550e8400-e29b-41d4-a716-446655440000");
    }

    [Fact]
    public void ExtractMentionedUserIds_WithMultipleMentions_ReturnsDistinctUserIds()
    {
        const string body = "@[Alice](user:aaaaaaaa-0000-0000-0000-000000000001) and @[Bob](user:bbbbbbbb-0000-0000-0000-000000000002) are assigned.";

        IReadOnlyList<string> result = MentionParser.ExtractMentionedUserIds(body);

        result.Count.ShouldBe(2);
        result.ShouldContain("aaaaaaaa-0000-0000-0000-000000000001");
        result.ShouldContain("bbbbbbbb-0000-0000-0000-000000000002");
    }

    [Fact]
    public void ExtractMentionedUserIds_WithDuplicateMention_ReturnsDistinct()
    {
        const string body = "@[Dr. Martin](user:550e8400-e29b-41d4-a716-446655440000) said hi. @[Dr. Martin](user:550e8400-e29b-41d4-a716-446655440000) confirmed.";

        IReadOnlyList<string> result = MentionParser.ExtractMentionedUserIds(body);

        result.Count.ShouldBe(1);
    }

    [Fact]
    public void ExtractMentionedUserIds_WithInvalidFormat_ReturnsEmpty()
    {
        const string body = "Hello @invalid and @[Name](wrong:123) are not valid mentions.";

        IReadOnlyList<string> result = MentionParser.ExtractMentionedUserIds(body);

        result.ShouldBeEmpty();
    }

    [Fact]
    public void ExtractMentionedUserIds_WithEmptyBody_ReturnsEmpty()
    {
        IReadOnlyList<string> result = MentionParser.ExtractMentionedUserIds(string.Empty);

        result.ShouldBeEmpty();
    }

    [Fact]
    public void ExtractMentionedUserIds_WithNullBody_ReturnsEmpty()
    {
        IReadOnlyList<string> result = MentionParser.ExtractMentionedUserIds(null!);

        result.ShouldBeEmpty();
    }

    [Fact]
    public void ExtractMentionedUserIds_WithMixedValidAndInvalid_ReturnsOnlyValid()
    {
        const string body = "@[Valid](user:12345678-1234-1234-1234-123456789012) and @[Invalid](user:not-a-guid) and @plain.";

        IReadOnlyList<string> result = MentionParser.ExtractMentionedUserIds(body);

        result.Count.ShouldBe(1);
        result[0].ShouldBe("12345678-1234-1234-1234-123456789012");
    }
}
