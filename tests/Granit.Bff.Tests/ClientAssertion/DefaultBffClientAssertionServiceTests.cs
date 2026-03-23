// =============================================================================
// Tests - DefaultBffClientAssertionService
// =============================================================================
// Verifies private_key_jwt client assertion creation (RFC 7523) for EC and RSA keys.
// =============================================================================

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Granit.Bff.ClientAssertion;
using Granit.Timing;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Bff.Tests.ClientAssertion;

public sealed class DefaultBffClientAssertionServiceTests
{
    private const string ClientId = "my-bff-client";
    private const string TokenEndpoint = "https://auth.example.com/connect/token";

    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly DefaultBffClientAssertionService _service;

    public DefaultBffClientAssertionServiceTests()
    {
        _clock.Now.Returns(new DateTimeOffset(2026, 3, 22, 12, 0, 0, TimeSpan.Zero));
        _service = new DefaultBffClientAssertionService(_clock);
    }

    // ── EC Key Assertions ──

    [Fact]
    public void CreateAssertion_WithEcKey_ReturnsValidJwt()
    {
        string jwk = GenerateEcP256Jwk();

        string assertion = _service.CreateAssertion(jwk, ClientId, TokenEndpoint);

        assertion.Split('.').Length.ShouldBe(3, "client assertion must be a three-part JWT");

        JsonElement header = DecodeJwtPart(assertion, 0);
        header.GetProperty("alg").GetString().ShouldBe("ES256");
        header.GetProperty("typ").GetString().ShouldBe("client-authentication+jwt");
    }

    [Fact]
    public void CreateAssertion_WithEcKey_ContainsRequiredClaims()
    {
        string jwk = GenerateEcP256Jwk();

        string assertion = _service.CreateAssertion(jwk, ClientId, TokenEndpoint);

        JsonElement payload = DecodeJwtPart(assertion, 1);
        payload.GetProperty("iss").GetString().ShouldBe(ClientId);
        payload.GetProperty("sub").GetString().ShouldBe(ClientId);
        payload.GetProperty("aud").GetString().ShouldBe(TokenEndpoint);
        payload.TryGetProperty("jti", out _).ShouldBeTrue("jti (JWT ID) is required");
        payload.TryGetProperty("iat", out _).ShouldBeTrue("iat (issued at) is required");
        payload.TryGetProperty("exp", out _).ShouldBeTrue("exp (expiration) is required");
    }

    [Fact]
    public void CreateAssertion_ExpiresAfter60Seconds()
    {
        string jwk = GenerateEcP256Jwk();

        string assertion = _service.CreateAssertion(jwk, ClientId, TokenEndpoint);

        JsonElement payload = DecodeJwtPart(assertion, 1);
        long iat = payload.GetProperty("iat").GetInt64();
        long exp = payload.GetProperty("exp").GetInt64();

        (exp - iat).ShouldBe(60);
    }

    [Fact]
    public void CreateAssertion_GeneratesUniqueJti()
    {
        string jwk = GenerateEcP256Jwk();

        string assertion1 = _service.CreateAssertion(jwk, ClientId, TokenEndpoint);
        string assertion2 = _service.CreateAssertion(jwk, ClientId, TokenEndpoint);

        string? jti1 = DecodeJwtPart(assertion1, 1).GetProperty("jti").GetString();
        string? jti2 = DecodeJwtPart(assertion2, 1).GetProperty("jti").GetString();

        jti1.ShouldNotBe(jti2);
    }

    // ── RSA Key Assertions ──

    [Fact]
    public void CreateAssertion_WithRsaKey_ReturnsValidJwt()
    {
        string jwk = GenerateRsa2048Jwk();

        string assertion = _service.CreateAssertion(jwk, ClientId, TokenEndpoint);

        assertion.Split('.').Length.ShouldBe(3, "client assertion must be a three-part JWT");

        JsonElement header = DecodeJwtPart(assertion, 0);
        header.GetProperty("alg").GetString().ShouldBe("PS256");
        header.GetProperty("typ").GetString().ShouldBe("client-authentication+jwt");
    }

    // ── Unsupported / Invalid Input ──

    [Fact]
    public void CreateAssertion_WithUnsupportedKeyType_Throws()
    {
        string octJwk = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["kty"] = "oct",
            ["k"] = Base64UrlEncode(RandomNumberGenerator.GetBytes(32)),
        });

        NotSupportedException exception = Should.Throw<NotSupportedException>(
            () => _service.CreateAssertion(octJwk, ClientId, TokenEndpoint));

        exception.Message.ShouldContain("oct");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void CreateAssertion_WithNullOrEmptyKey_ThrowsArgumentException(string? privateKeyJwk)
    {
        Should.Throw<ArgumentException>(
            () => _service.CreateAssertion(privateKeyJwk!, ClientId, TokenEndpoint));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void CreateAssertion_WithNullOrEmptyClientId_ThrowsArgumentException(string? clientId)
    {
        string jwk = GenerateEcP256Jwk();

        Should.Throw<ArgumentException>(
            () => _service.CreateAssertion(jwk, clientId!, TokenEndpoint));
    }

    // ── Signature Verification ──

    [Fact]
    public void CreateAssertion_EcSignatureIsVerifiable()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        ECParameters ecParams = ecdsa.ExportParameters(true);
        string jwk = SerializeEcJwk(ecParams);

        string assertion = _service.CreateAssertion(jwk, ClientId, TokenEndpoint);

        // Verify using the public key
        using var verifier = ECDsa.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = ecParams.Q,
        });

        string[] parts = assertion.Split('.');
        byte[] signingInput = Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}");
        byte[] signature = Base64UrlDecode(parts[2]);

        verifier.VerifyData(signingInput, signature, HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation).ShouldBeTrue("ES256 signature must be valid");
    }

    [Fact]
    public void CreateAssertion_RsaSignatureIsVerifiable()
    {
        using var rsa = RSA.Create(2048);
        RSAParameters rsaParams = rsa.ExportParameters(true);
        string jwk = SerializeRsaJwk(rsaParams);

        string assertion = _service.CreateAssertion(jwk, ClientId, TokenEndpoint);

        // Verify using the public key
        using var verifier = RSA.Create(new RSAParameters
        {
            Modulus = rsaParams.Modulus,
            Exponent = rsaParams.Exponent,
        });

        string[] parts = assertion.Split('.');
        byte[] signingInput = Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}");
        byte[] signature = Base64UrlDecode(parts[2]);

        verifier.VerifyData(signingInput, signature, HashAlgorithmName.SHA256,
            RSASignaturePadding.Pss).ShouldBeTrue("PS256 signature must be valid");
    }

    // ── Helpers ──

    private static string GenerateEcP256Jwk()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        return SerializeEcJwk(ecdsa.ExportParameters(true));
    }

    private static string SerializeEcJwk(ECParameters ecParams) =>
        JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["kty"] = "EC",
            ["crv"] = "P-256",
            ["x"] = Base64UrlEncode(ecParams.Q.X!),
            ["y"] = Base64UrlEncode(ecParams.Q.Y!),
            ["d"] = Base64UrlEncode(ecParams.D!),
        });

    private static string GenerateRsa2048Jwk()
    {
        using var rsa = RSA.Create(2048);
        return SerializeRsaJwk(rsa.ExportParameters(true));
    }

    private static string SerializeRsaJwk(RSAParameters rsaParams) =>
        JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["kty"] = "RSA",
            ["n"] = Base64UrlEncode(rsaParams.Modulus!),
            ["e"] = Base64UrlEncode(rsaParams.Exponent!),
            ["d"] = Base64UrlEncode(rsaParams.D!),
            ["p"] = Base64UrlEncode(rsaParams.P!),
            ["q"] = Base64UrlEncode(rsaParams.Q!),
            ["dp"] = Base64UrlEncode(rsaParams.DP!),
            ["dq"] = Base64UrlEncode(rsaParams.DQ!),
            ["qi"] = Base64UrlEncode(rsaParams.InverseQ!),
        });

    private static JsonElement DecodeJwtPart(string jwt, int partIndex)
    {
        string part = jwt.Split('.')[partIndex];
        byte[] bytes = Base64UrlDecode(part);
        using var doc = JsonDocument.Parse(bytes);
        return doc.RootElement.Clone();
    }

    private static string Base64UrlEncode(byte[] data) =>
        Convert.ToBase64String(data)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

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
