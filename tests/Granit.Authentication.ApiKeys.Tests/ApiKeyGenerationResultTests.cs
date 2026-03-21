using Shouldly;
using Xunit;

namespace Granit.Authentication.ApiKeys.Tests;

public sealed class ApiKeyGenerationResultTests
{
    [Fact]
    public void Record_HasCorrectProperties()
    {
        ApiKeyGenerationResult result = new(
            "gk_live_sk_abc123",
            "sha256hash",
            "gk_live_sk_",
            "c123");

        result.RawSecret.ShouldBe("gk_live_sk_abc123");
        result.HashedKey.ShouldBe("sha256hash");
        result.Prefix.ShouldBe("gk_live_sk_");
        result.LastFourChars.ShouldBe("c123");
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        ApiKeyGenerationResult a = new("raw", "hash", "prefix", "last");
        ApiKeyGenerationResult b = new("raw", "hash", "prefix", "last");

        a.ShouldBe(b);
    }

    [Fact]
    public void Equality_DifferentValues_AreNotEqual()
    {
        ApiKeyGenerationResult a = new("raw1", "hash1", "prefix", "last");
        ApiKeyGenerationResult b = new("raw2", "hash2", "prefix", "last");

        a.ShouldNotBe(b);
    }
}
