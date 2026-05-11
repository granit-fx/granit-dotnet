using System.Text;
using Granit.Documents.PublicLinks.Domain;
using Shouldly;
using Xunit;

namespace Granit.Documents.PublicLinks.Tests;

public sealed class PublicLinkTokenFactoryTests
{
    [Fact]
    public void GenerateRandom_ProducesUrlSafeBase64()
    {
        PublicLinkToken token = PublicLinkTokenFactory.GenerateRandom();

        token.Value.ShouldNotBeNullOrEmpty();
        token.Value.Length.ShouldBe(PublicLinkToken.EncodedLength);
        token.Value.ShouldNotContain("=");
        token.Value.ShouldNotContain("+");
        token.Value.ShouldNotContain("/");
        foreach (char c in token.Value)
        {
            bool isUrlSafe = char.IsLetterOrDigit(c) || c is '-' or '_';
            isUrlSafe.ShouldBeTrue($"Unexpected character '{c}' in URL-safe base64 token.");
        }
    }

    [Fact]
    public void GenerateRandom_IsUniqueAcrossCalls()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < 100; i++)
        {
            seen.Add(PublicLinkTokenFactory.GenerateRandom().Value).ShouldBeTrue();
        }
    }

    [Fact]
    public void ComputeHash_IsDeterministic()
    {
        byte[] pepper = Encoding.UTF8.GetBytes("super-secret-pepper");
        const string token = "abc-123";

        byte[] first = PublicLinkTokenFactory.ComputeHash(token, pepper);
        byte[] second = PublicLinkTokenFactory.ComputeHash(token, pepper);

        first.Length.ShouldBe(32); // HMAC-SHA256
        first.ShouldBe(second);
    }

    [Fact]
    public void ComputeHash_ChangesWithPepper()
    {
        const string token = "abc-123";
        byte[] hashA = PublicLinkTokenFactory.ComputeHash(token, [1, 2, 3]);
        byte[] hashB = PublicLinkTokenFactory.ComputeHash(token, [4, 5, 6]);
        hashA.ShouldNotBe(hashB);
    }

    [Fact]
    public void ComputeHash_RejectsEmptyPepper()
    {
        Should.Throw<ArgumentException>(() =>
            PublicLinkTokenFactory.ComputeHash("token", []));
    }

    [Fact]
    public void ComputeHash_RejectsEmptyToken()
    {
        Should.Throw<ArgumentException>(() =>
            PublicLinkTokenFactory.ComputeHash(string.Empty, [1, 2, 3]));
    }
}
