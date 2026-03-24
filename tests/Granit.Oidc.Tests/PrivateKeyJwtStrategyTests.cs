using System.Text;
using System.Text.Json;
using Granit.Oidc.ClientAuthentication.Internal;
using Granit.Timing;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Oidc.Tests;

public sealed class PrivateKeyJwtStrategyTests
{
    private readonly IClock _clock = Substitute.For<IClock>();

    public PrivateKeyJwtStrategyTests()
    {
        _clock.Now.Returns(new DateTimeOffset(2026, 3, 15, 10, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void Apply_AddsClientAssertionParameters()
    {
        string privateKeyJwk = GenerateEcKeyJwk();
        var strategy = new PrivateKeyJwtStrategy(privateKeyJwk, _clock);
        var parameters = new Dictionary<string, string>();

        strategy.Apply(parameters, "my-client", "https://idp.example.com/token");

        parameters.ShouldContainKey(OidcConstants.Parameters.ClientAssertion);
        parameters.ShouldContainKey(OidcConstants.Parameters.ClientAssertionType);
        parameters[OidcConstants.Parameters.ClientAssertionType]
            .ShouldBe(OidcConstants.ClientAssertionTypes.JwtBearer);
    }

    [Fact]
    public void Apply_GeneratesValidJwt()
    {
        string privateKeyJwk = GenerateEcKeyJwk();
        var strategy = new PrivateKeyJwtStrategy(privateKeyJwk, _clock);
        var parameters = new Dictionary<string, string>();

        strategy.Apply(parameters, "my-client", "https://idp.example.com/token");

        string jwt = parameters[OidcConstants.Parameters.ClientAssertion];
        string[] parts = jwt.Split('.');
        parts.Length.ShouldBe(3);

        // Parse and verify payload claims
        string payloadJson = DecodeBase64Url(parts[1]);
        using var doc = JsonDocument.Parse(payloadJson);
        JsonElement root = doc.RootElement;

        root.GetProperty("iss").GetString().ShouldBe("my-client");
        root.GetProperty("sub").GetString().ShouldBe("my-client");
        root.GetProperty("aud").GetString().ShouldBe("https://idp.example.com/token");
        root.TryGetProperty("jti", out _).ShouldBeTrue();
        root.TryGetProperty("iat", out _).ShouldBeTrue();
        root.TryGetProperty("exp", out _).ShouldBeTrue();
    }

    [Fact]
    public void Apply_JwtExpiresAfterIssuedAt()
    {
        string privateKeyJwk = GenerateEcKeyJwk();
        var strategy = new PrivateKeyJwtStrategy(privateKeyJwk, _clock);
        var parameters = new Dictionary<string, string>();

        strategy.Apply(parameters, "my-client", "https://idp.example.com/token");

        string jwt = parameters[OidcConstants.Parameters.ClientAssertion];
        string payloadJson = DecodeBase64Url(jwt.Split('.')[1]);
        using var doc = JsonDocument.Parse(payloadJson);

        long iat = doc.RootElement.GetProperty("iat").GetInt64();
        long exp = doc.RootElement.GetProperty("exp").GetInt64();

        exp.ShouldBeGreaterThan(iat);
    }

    private static string GenerateEcKeyJwk()
    {
        using var ecdsa = System.Security.Cryptography.ECDsa.Create(
            System.Security.Cryptography.ECCurve.NamedCurves.nistP256);
        System.Security.Cryptography.ECParameters parameters = ecdsa.ExportParameters(includePrivateParameters: true);

        var jwk = new Dictionary<string, string>
        {
            ["kty"] = "EC",
            ["crv"] = "P-256",
            ["x"] = EncodeBase64Url(parameters.Q.X!),
            ["y"] = EncodeBase64Url(parameters.Q.Y!),
            ["d"] = EncodeBase64Url(parameters.D!),
        };

        return JsonSerializer.Serialize(jwk);
    }

    private static string EncodeBase64Url(byte[] data) =>
        Convert.ToBase64String(data)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

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
