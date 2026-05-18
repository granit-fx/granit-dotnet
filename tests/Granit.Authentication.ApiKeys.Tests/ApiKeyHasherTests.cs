using Granit.Authentication.ApiKeys.Internal;
using Granit.Authentication.ApiKeys.Options;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Authentication.ApiKeys.Tests;

public sealed class ApiKeyHasherTests
{
    private static ApiKeyHasher Create(string? pepper = null) =>
        new(Microsoft.Extensions.Options.Options.Create(new ApiKeysOptions { Pepper = pepper }));

    [Fact]
    public void Current_WithoutPepper_IsV1Sha256()
    {
        ApiKeyHasher sut = Create();

        string hash = sut.ComputeCurrentHash("gk_test_sk_abc");

        hash.ShouldStartWith("v1$");
        // SHA-256 hex = 64 chars + 3 prefix chars
        hash.Length.ShouldBe(67);
    }

    [Fact]
    public void Current_WithPepper_IsV2Hmac()
    {
        string pepper = Convert.ToBase64String(new byte[32]);
        ApiKeyHasher sut = Create(pepper);

        string hash = sut.ComputeCurrentHash("gk_test_sk_abc");

        hash.ShouldStartWith("v2$");
        hash.Length.ShouldBe(67);
    }

    [Fact]
    public void Current_DifferentPeppers_ProduceDifferentHashes()
    {
        // Pepper rotation produces different v2 hashes for the same raw key.
        string pepperA = Convert.ToBase64String(Enumerable.Range(0, 32).Select(i => (byte)i).ToArray());
        string pepperB = Convert.ToBase64String(Enumerable.Range(32, 32).Select(i => (byte)i).ToArray());

        string hashA = Create(pepperA).ComputeCurrentHash("gk_test_sk_abc");
        string hashB = Create(pepperB).ComputeCurrentHash("gk_test_sk_abc");

        hashA.ShouldNotBe(hashB);
    }

    [Fact]
    public void Candidates_WithoutPepper_ReturnsOnlyV1()
    {
        IReadOnlyList<string> candidates = Create().ComputeCandidateHashes("gk_test_sk_abc");

        candidates.Count.ShouldBe(1);
        candidates[0].ShouldStartWith("v1$");
    }

    [Fact]
    public void Candidates_WithPepper_ReturnsV2ThenV1()
    {
        // Auth handler uses this list to look up keys hashed before the pepper
        // was configured (v1) AND keys hashed after (v2) with a single hasher.
        string pepper = Convert.ToBase64String(new byte[32]);
        IReadOnlyList<string> candidates = Create(pepper).ComputeCandidateHashes("gk_test_sk_abc");

        candidates.Count.ShouldBe(2);
        candidates[0].ShouldStartWith("v2$");
        candidates[1].ShouldStartWith("v1$");
    }

    [Fact]
    public void Hashing_IsDeterministic()
    {
        string pepper = Convert.ToBase64String(new byte[32]);
        ApiKeyHasher sut = Create(pepper);

        sut.ComputeCurrentHash("input").ShouldBe(sut.ComputeCurrentHash("input"));
    }

    [Fact]
    public void ComputeCurrentHash_ThrowsOnEmptyInput()
        => Should.Throw<ArgumentException>(() => Create().ComputeCurrentHash(string.Empty));
}
