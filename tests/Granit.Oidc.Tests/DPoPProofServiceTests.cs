using System.Text;
using System.Text.Json;
using Granit.Oidc.DPoP.Internal;
using Granit.Timing;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Oidc.Tests;

public sealed class DPoPProofServiceTests
{
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly DPoPProofService _sut;

    public DPoPProofServiceTests()
    {
        _clock.Now.Returns(DateTimeOffset.UtcNow);
        _sut = new DPoPProofService(_clock);
    }

    [Fact]
    public void GenerateKeyPair_ReturnsValidJwk()
    {
        string jwk = _sut.GenerateKeyPair();

        using var doc = JsonDocument.Parse(jwk);
        JsonElement root = doc.RootElement;

        root.GetProperty("kty").GetString().ShouldBe("EC");
        root.GetProperty("crv").GetString().ShouldBe("P-256");
        root.TryGetProperty("x", out _).ShouldBeTrue();
        root.TryGetProperty("y", out _).ShouldBeTrue();
        root.TryGetProperty("d", out _).ShouldBeTrue();
    }

    [Fact]
    public void CreateProof_ReturnsThreePartJwt()
    {
        string privateKeyJwk = _sut.GenerateKeyPair();

        string proof = _sut.CreateProof(privateKeyJwk, "POST", "https://server.example.com/token");

        string[] parts = proof.Split('.');
        parts.Length.ShouldBe(3);
        parts[0].ShouldNotBeNullOrEmpty();
        parts[1].ShouldNotBeNullOrEmpty();
        parts[2].ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void CreateProof_IncludesNonceWhenProvided()
    {
        string privateKeyJwk = _sut.GenerateKeyPair();

        string proof = _sut.CreateProof(
            privateKeyJwk, "POST", "https://server.example.com/token", nonce: "server-nonce-42");

        string payloadJson = DecodeJwtPayload(proof);
        using var doc = JsonDocument.Parse(payloadJson);

        doc.RootElement.GetProperty("nonce").GetString().ShouldBe("server-nonce-42");
    }

    [Fact]
    public void CreateProof_StripsQueryFromUri()
    {
        string privateKeyJwk = _sut.GenerateKeyPair();

        string proof = _sut.CreateProof(
            privateKeyJwk, "GET", "https://api.example.com/resource?page=1&size=10");

        string payloadJson = DecodeJwtPayload(proof);
        using var doc = JsonDocument.Parse(payloadJson);

        string htu = doc.RootElement.GetProperty("htu").GetString()!;
        htu.ShouldBe("https://api.example.com/resource");
        htu.ShouldNotContain("?");
    }

    [Fact]
    public void CreateProof_HeaderContainsCorrectTypAndAlg()
    {
        string privateKeyJwk = _sut.GenerateKeyPair();

        string proof = _sut.CreateProof(privateKeyJwk, "POST", "https://server.example.com/token");

        string headerJson = DecodeJwtHeader(proof);
        using var doc = JsonDocument.Parse(headerJson);

        doc.RootElement.GetProperty("typ").GetString().ShouldBe("dpop+jwt");
        doc.RootElement.GetProperty("alg").GetString().ShouldBe("ES256");
        doc.RootElement.TryGetProperty("jwk", out _).ShouldBeTrue();
    }

    [Fact]
    public void CreateProof_PayloadContainsRequiredClaims()
    {
        _clock.Now.Returns(new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero));
        string privateKeyJwk = _sut.GenerateKeyPair();

        string proof = _sut.CreateProof(privateKeyJwk, "POST", "https://server.example.com/token");

        string payloadJson = DecodeJwtPayload(proof);
        using var doc = JsonDocument.Parse(payloadJson);
        JsonElement root = doc.RootElement;

        root.TryGetProperty("jti", out _).ShouldBeTrue();
        root.GetProperty("htm").GetString().ShouldBe("POST");
        root.GetProperty("htu").GetString().ShouldBe("https://server.example.com/token");
        root.TryGetProperty("iat", out _).ShouldBeTrue();
        root.TryGetProperty("exp", out _).ShouldBeTrue();
    }

    [Fact]
    public void CreateProof_OmitsNonceWhenNull()
    {
        string privateKeyJwk = _sut.GenerateKeyPair();

        string proof = _sut.CreateProof(privateKeyJwk, "POST", "https://server.example.com/token");

        string payloadJson = DecodeJwtPayload(proof);
        using var doc = JsonDocument.Parse(payloadJson);

        doc.RootElement.TryGetProperty("nonce", out _).ShouldBeFalse();
    }

    private static string DecodeJwtPayload(string jwt)
    {
        string[] parts = jwt.Split('.');
        return DecodeBase64Url(parts[1]);
    }

    private static string DecodeJwtHeader(string jwt)
    {
        string[] parts = jwt.Split('.');
        return DecodeBase64Url(parts[0]);
    }

    private static string DecodeBase64Url(string base64Url)
    {
        string padded = base64Url.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2: padded += "=="; break;
            case 3: padded += "="; break;
        }

        return Encoding.UTF8.GetString(Convert.FromBase64String(padded));
    }
}
