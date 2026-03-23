// =============================================================================
// Tests — JwkThumbprintCalculator (RFC 7638)
// =============================================================================
// Validates JWK Thumbprint computation with RFC 7638 §3.1 test vector
// and EC P-256 keys.
// =============================================================================

using System.Security.Cryptography;
using System.Text.Json;
using Granit.Authentication.DPoP.Validation.Internal;
using Shouldly;
using Xunit;

namespace Granit.Authentication.DPoP.Tests.Validation;

public sealed class JwkThumbprintCalculatorTests
{
    // ── RFC 7638 §3.1 test vector ──

    [Fact]
    public void ComputeThumbprint_Rfc7638TestVector_MatchesExpectedValue()
    {
        // RFC 7638 §3.1 example RSA key (required members only)
        // Expected thumbprint: NzbLsXh8uDCcd-6MNwXF4W_7noWXFZAfHkxZsRGC9Xs
        string jwkJson = """
        {
            "kty": "RSA",
            "n": "0vx7agoebGcQSuuPiLJXZptN9nndrQmbXEps2aiAFbWhM78LhWx4cbbfAAtVT86zwu1RK7aPFFxuhDR1L6tSoc_BJECPebWKRXjBZCiFV4n3oknjhMstn64tZ_2W-5JsGY4Hc5n9yBXArwl93lqt7_RN5w6Cf0h4QyQ5v-65YGjQR0_FDW2QvzqY368QQMicAtaSqzs8KJZgnYb9c7d0zgdAZHzu6qMQvRL5hajrn1n91CbOpbISD08qNLyrdkt-bFTWhAI4vMQFh6WeZu0fM4lFd2NcRwr3XPksINHaQ-G_xBniIqbw0Ls1jF44-csFCur-kEgU8awapJzKnqDKgw",
            "e": "AQAB"
        }
        """;

        using var doc = JsonDocument.Parse(jwkJson);
        string thumbprint = JwkThumbprintCalculator.ComputeThumbprint(doc.RootElement);

        thumbprint.ShouldBe("NzbLsXh8uDCcd-6MNwXF4W_7noWXFZAfHkxZsRGC9Xs");
    }

    // ── EC P-256 thumbprint ──

    [Fact]
    public void ComputeThumbprint_EcP256Key_ProducesConsistentResult()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        ECParameters parameters = ecdsa.ExportParameters(includePrivateParameters: false);

        string x = Base64UrlEncode(parameters.Q.X!);
        string y = Base64UrlEncode(parameters.Q.Y!);

        string jwkJson = $$"""{"crv":"P-256","kty":"EC","x":"{{x}}","y":"{{y}}"}""";

        using var doc = JsonDocument.Parse(jwkJson);
        string thumbprint1 = JwkThumbprintCalculator.ComputeThumbprint(doc.RootElement);
        string thumbprint2 = JwkThumbprintCalculator.ComputeThumbprint(doc.RootElement);

        thumbprint1.ShouldBe(thumbprint2, "Thumbprint must be deterministic");
        thumbprint1.ShouldNotBeNullOrEmpty();
        thumbprint1.Length.ShouldBe(43, "SHA-256 base64url is always 43 chars");
    }

    [Fact]
    public void ComputeThumbprint_EcKey_IgnoresPrivateKeyParameter()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        ECParameters pubParams = ecdsa.ExportParameters(includePrivateParameters: false);
        ECParameters privParams = ecdsa.ExportParameters(includePrivateParameters: true);

        string x = Base64UrlEncode(pubParams.Q.X!);
        string y = Base64UrlEncode(pubParams.Q.Y!);
        string d = Base64UrlEncode(privParams.D!);

        string publicJwk = $$"""{"crv":"P-256","kty":"EC","x":"{{x}}","y":"{{y}}"}""";
        string privateJwk = $$"""{"crv":"P-256","d":"{{d}}","kty":"EC","x":"{{x}}","y":"{{y}}"}""";

        using var pubDoc = JsonDocument.Parse(publicJwk);
        using var privDoc = JsonDocument.Parse(privateJwk);

        string pubThumbprint = JwkThumbprintCalculator.ComputeThumbprint(pubDoc.RootElement);
        string privThumbprint = JwkThumbprintCalculator.ComputeThumbprint(privDoc.RootElement);

        pubThumbprint.ShouldBe(privThumbprint, "Private key params must not affect thumbprint");
    }

    [Fact]
    public void ComputeThumbprint_DifferentKeys_ProduceDifferentThumbprints()
    {
        using var key1 = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var key2 = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        ECParameters p1 = key1.ExportParameters(false);
        ECParameters p2 = key2.ExportParameters(false);

        string jwk1 = $$"""{"crv":"P-256","kty":"EC","x":"{{Base64UrlEncode(p1.Q.X!)}}","y":"{{Base64UrlEncode(p1.Q.Y!)}}"}""";
        string jwk2 = $$"""{"crv":"P-256","kty":"EC","x":"{{Base64UrlEncode(p2.Q.X!)}}","y":"{{Base64UrlEncode(p2.Q.Y!)}}"}""";

        using var doc1 = JsonDocument.Parse(jwk1);
        using var doc2 = JsonDocument.Parse(jwk2);

        string t1 = JwkThumbprintCalculator.ComputeThumbprint(doc1.RootElement);
        string t2 = JwkThumbprintCalculator.ComputeThumbprint(doc2.RootElement);

        t1.ShouldNotBe(t2, "Different keys must produce different thumbprints");
    }

    [Fact]
    public void ComputeThumbprint_UnsupportedKeyType_Throws()
    {
        string jwkJson = """{"kty":"oct","k":"AyM32w"}""";

        using var doc = JsonDocument.Parse(jwkJson);
        Should.Throw<NotSupportedException>(() =>
            JwkThumbprintCalculator.ComputeThumbprint(doc.RootElement));
    }

    private static string Base64UrlEncode(byte[] data) =>
        Convert.ToBase64String(data)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
}
