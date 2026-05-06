using Granit.Authentication.ApiKeys.Internal;
using Granit.Authentication.ApiKeys.Options;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Authentication.ApiKeys.Tests;

public sealed class ApiKeyGeneratorTests
{
    private static ApiKeyGenerator Create(string? pepper = null)
    {
        IApiKeyHasher hasher = new ApiKeyHasher(
            Microsoft.Extensions.Options.Options.Create(new ApiKeysOptions { Pepper = pepper }));
        return new ApiKeyGenerator(hasher);
    }

    [Theory]
    [InlineData(ApiKeyType.Secret, "live", "gk_live_sk_")]
    [InlineData(ApiKeyType.Publishable, "live", "gk_live_pk_")]
    [InlineData(ApiKeyType.Webhook, "test", "gk_test_wh_")]
    [InlineData(ApiKeyType.Ephemeral, "dev", "gk_dev_ep_")]
    public void Generate_ProducesCorrectPrefix(ApiKeyType type, string env, string expectedPrefix)
    {
        ApiKeyGenerationResult result = Create().Generate(type, env);

        result.RawSecret.ShouldStartWith(expectedPrefix);
        result.Prefix.ShouldBe(expectedPrefix);
    }

    [Fact]
    public void Generate_RawSecretHasSufficientLength()
    {
        ApiKeyGenerationResult result = Create().Generate(ApiKeyType.Secret, "live");

        // Prefix (11 chars "gk_live_sk_") + 32 random chars = 43 characters
        result.RawSecret.Length.ShouldBeGreaterThanOrEqualTo(43);
    }

    [Fact]
    public void Generate_WithoutPepper_HashedKeyIsV1()
    {
        ApiKeyGenerationResult result = Create().Generate(ApiKeyType.Secret, "live");

        result.HashedKey.ShouldStartWith("v1$");
    }

    [Fact]
    public void Generate_WithPepper_HashedKeyIsV2()
    {
        // 32-byte pepper, base64-encoded.
        string pepper = Convert.ToBase64String(new byte[32]);
        ApiKeyGenerationResult result = Create(pepper).Generate(ApiKeyType.Secret, "live");

        result.HashedKey.ShouldStartWith("v2$");
    }

    [Fact]
    public void Generate_LastFourCharsMatchRawSecret()
    {
        ApiKeyGenerationResult result = Create().Generate(ApiKeyType.Secret, "live");

        result.LastFourChars.ShouldBe(result.RawSecret[^4..]);
    }

    [Fact]
    public void Generate_ProducesUniqueKeys()
    {
        ApiKeyGenerator sut = Create();
        var hashes = new HashSet<string>();

        for (int i = 0; i < 1000; i++)
        {
            ApiKeyGenerationResult result = sut.Generate(ApiKeyType.Secret, "live");
            hashes.Add(result.HashedKey).ShouldBeTrue($"Duplicate key at iteration {i}");
        }
    }

    [Fact]
    public void Generate_ThrowsOnNullEnvironment() =>
        Should.Throw<ArgumentNullException>(() => Create().Generate(ApiKeyType.Secret, null!));
}
