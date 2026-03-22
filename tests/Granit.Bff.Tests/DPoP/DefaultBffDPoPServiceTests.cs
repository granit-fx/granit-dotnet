// =============================================================================
// Unit Tests — DefaultBffDPoPService
// =============================================================================
// Verifies EC P-256 key generation and DPoP proof JWT creation (RFC 9449).
// =============================================================================

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Granit.Bff.DPoP;
using Granit.Timing;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Bff.Tests.DPoP;

public sealed class DefaultBffDPoPServiceTests
{
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly DefaultBffDPoPService _service;

    public DefaultBffDPoPServiceTests()
    {
        _clock.Now.Returns(new DateTimeOffset(2026, 3, 22, 12, 0, 0, TimeSpan.Zero));
        _service = new DefaultBffDPoPService(_clock);
    }

    // ── Key Generation ──

    [Fact]
    public void GenerateKeyPair_Returns_Valid_Ec_P256_Jwk()
    {
        string jwk = _service.GenerateKeyPair();

        using var doc = JsonDocument.Parse(jwk);
        JsonElement root = doc.RootElement;

        root.GetProperty("kty").GetString().ShouldBe("EC");
        root.GetProperty("crv").GetString().ShouldBe("P-256");
        root.TryGetProperty("x", out _).ShouldBeTrue();
        root.TryGetProperty("y", out _).ShouldBeTrue();
        root.TryGetProperty("d", out _).ShouldBeTrue("private key parameter must be present");
    }

    [Fact]
    public void GenerateKeyPair_Produces_Unique_Keys()
    {
        string key1 = _service.GenerateKeyPair();
        string key2 = _service.GenerateKeyPair();

        key1.ShouldNotBe(key2);
    }

    // ── Proof Creation ──

    [Fact]
    public void CreateProof_Returns_Three_Part_Jwt()
    {
        string privateKeyJwk = _service.GenerateKeyPair();

        string proof = _service.CreateProof(privateKeyJwk, "POST", "https://auth.example.com/connect/token");

        proof.Split('.').Length.ShouldBe(3, "DPoP proof must be a three-part JWT");
    }

    [Fact]
    public void CreateProof_Header_Contains_Required_Fields()
    {
        string privateKeyJwk = _service.GenerateKeyPair();

        string proof = _service.CreateProof(privateKeyJwk, "POST", "https://auth.example.com/connect/token");

        JsonElement header = DecodeJwtPart(proof, 0);
        header.GetProperty("typ").GetString().ShouldBe("dpop+jwt");
        header.GetProperty("alg").GetString().ShouldBe("ES256");

        JsonElement jwk = header.GetProperty("jwk");
        jwk.GetProperty("kty").GetString().ShouldBe("EC");
        jwk.GetProperty("crv").GetString().ShouldBe("P-256");
        jwk.TryGetProperty("x", out _).ShouldBeTrue();
        jwk.TryGetProperty("y", out _).ShouldBeTrue();
        jwk.TryGetProperty("d", out _).ShouldBeFalse("public key in header must NOT contain private key");
    }

    [Fact]
    public void CreateProof_Payload_Contains_Required_Claims()
    {
        string privateKeyJwk = _service.GenerateKeyPair();

        string proof = _service.CreateProof(privateKeyJwk, "GET", "https://api.example.com/resource");

        JsonElement payload = DecodeJwtPart(proof, 1);
        payload.TryGetProperty("jti", out _).ShouldBeTrue("jti (JWT ID) is required");
        payload.GetProperty("htm").GetString().ShouldBe("GET");
        payload.GetProperty("htu").GetString().ShouldBe("https://api.example.com/resource");
        payload.TryGetProperty("iat", out _).ShouldBeTrue("iat (issued at) is required");
        payload.TryGetProperty("exp", out _).ShouldBeTrue("exp (expiration) is required");
    }

    [Fact]
    public void CreateProof_Exp_Is_30_Seconds_After_Iat()
    {
        string privateKeyJwk = _service.GenerateKeyPair();

        string proof = _service.CreateProof(privateKeyJwk, "POST", "https://auth.example.com/connect/token");

        JsonElement payload = DecodeJwtPart(proof, 1);
        long iat = payload.GetProperty("iat").GetInt64();
        long exp = payload.GetProperty("exp").GetInt64();

        (exp - iat).ShouldBe(30);
    }

    [Fact]
    public void CreateProof_Htm_Is_Uppercased()
    {
        string privateKeyJwk = _service.GenerateKeyPair();

        string proof = _service.CreateProof(privateKeyJwk, "post", "https://auth.example.com/connect/token");

        JsonElement payload = DecodeJwtPart(proof, 1);
        payload.GetProperty("htm").GetString().ShouldBe("POST");
    }

    [Fact]
    public void CreateProof_Htu_Strips_Query_String()
    {
        string privateKeyJwk = _service.GenerateKeyPair();

        string proof = _service.CreateProof(privateKeyJwk, "GET", "https://api.example.com/resource?page=1&size=10");

        JsonElement payload = DecodeJwtPart(proof, 1);
        payload.GetProperty("htu").GetString().ShouldBe("https://api.example.com/resource");
    }

    [Fact]
    public void CreateProof_Generates_Unique_Jti_Per_Call()
    {
        string privateKeyJwk = _service.GenerateKeyPair();

        string proof1 = _service.CreateProof(privateKeyJwk, "POST", "https://auth.example.com/connect/token");
        string proof2 = _service.CreateProof(privateKeyJwk, "POST", "https://auth.example.com/connect/token");

        string? jti1 = DecodeJwtPart(proof1, 1).GetProperty("jti").GetString();
        string? jti2 = DecodeJwtPart(proof2, 1).GetProperty("jti").GetString();

        jti1.ShouldNotBe(jti2);
    }

    [Fact]
    public void CreateProof_Signature_Is_Valid_ES256()
    {
        string privateKeyJwk = _service.GenerateKeyPair();

        string proof = _service.CreateProof(privateKeyJwk, "POST", "https://auth.example.com/connect/token");

        // Extract public key from header
        JsonElement header = DecodeJwtPart(proof, 0);
        JsonElement jwk = header.GetProperty("jwk");
        byte[] x = Base64UrlDecode(jwk.GetProperty("x").GetString()!);
        byte[] y = Base64UrlDecode(jwk.GetProperty("y").GetString()!);

        using var ecdsa = ECDsa.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint { X = x, Y = y },
        });

        string[] parts = proof.Split('.');
        byte[] signingInput = Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}");
        byte[] signature = Base64UrlDecode(parts[2]);

        ecdsa.VerifyData(signingInput, signature, HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation).ShouldBeTrue("ES256 signature must be valid");
    }

    // ── Helpers ──

    private static JsonElement DecodeJwtPart(string jwt, int partIndex)
    {
        string part = jwt.Split('.')[partIndex];
        byte[] bytes = Base64UrlDecode(part);
        using var doc = JsonDocument.Parse(bytes);
        return doc.RootElement.Clone();
    }

    private static byte[] Base64UrlDecode(string base64Url)
    {
        string padded = base64Url.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2: padded += "=="; break;
            case 3: padded += "="; break;
        }

        return Convert.FromBase64String(padded);
    }
}
