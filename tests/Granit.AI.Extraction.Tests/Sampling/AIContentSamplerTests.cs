using Granit.AI.Extraction.Sampling;
using Shouldly;

namespace Granit.AI.Extraction.Tests.Sampling;

public sealed class AIContentSamplerTests
{
    [Fact]
    public void Returns_content_unchanged_when_shorter_than_cap()
    {
        AIContentSampler.TruncateOnCodePoint("short", 100).ShouldBe("short");
    }

    [Fact]
    public void Returns_content_unchanged_when_exactly_at_cap()
    {
        AIContentSampler.TruncateOnCodePoint("abcde", 5).ShouldBe("abcde");
    }

    [Fact]
    public void Truncates_to_cap_on_plain_ascii()
    {
        AIContentSampler.TruncateOnCodePoint("abcdefghij", 4).ShouldBe("abcd");
    }

    [Fact]
    public void Backs_off_one_unit_when_cut_would_split_a_surrogate_pair()
    {
        // 🚀 = U+1F680 = surrogate pair D83D DE80 (2 UTF-16 code units).
        // "abcdefghijk🚀tail" — a cut at 12 lands between D83D and DE80; the helper
        // must trim to 11 so the result is valid UTF-16.
        string result = AIContentSampler.TruncateOnCodePoint("abcdefghijk🚀tail", 12);

        result.ShouldBe("abcdefghijk");
        result.ShouldNotMatch(@"[\uD800-\uDBFF](?![\uDC00-\uDFFF])");
    }

    [Fact]
    public void Keeps_full_surrogate_pair_when_cut_falls_after_the_low_surrogate()
    {
        // Cut at 13 includes both halves of the pair → keep the rocket.
        string result = AIContentSampler.TruncateOnCodePoint("abcdefghijk🚀tail", 13);

        result.ShouldBe("abcdefghijk🚀");
    }

    [Fact]
    public void Rejects_negative_cap()
    {
        Should.Throw<ArgumentOutOfRangeException>(
            () => AIContentSampler.TruncateOnCodePoint("x", -1));
    }

    [Fact]
    public void Rejects_null_content()
    {
        Should.Throw<ArgumentNullException>(
            () => AIContentSampler.TruncateOnCodePoint(null!, 10));
    }
}
