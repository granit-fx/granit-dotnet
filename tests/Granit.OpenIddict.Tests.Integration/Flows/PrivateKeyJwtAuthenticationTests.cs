using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Granit.OpenIddict.Tests.Integration.Fixtures;
using Granit.OpenIddict.Tests.Integration.Helpers;
using Shouldly;
using Xunit;

namespace Granit.OpenIddict.Tests.Integration.Flows;

/// <summary>
/// Integration tests for <c>private_key_jwt</c> client authentication (RFC 7523).
/// Verifies that OpenIddict accepts client assertions signed with the registered public key
/// and rejects invalid, expired, or wrongly-signed assertions.
/// </summary>
[Collection("openiddict-integration")]
public sealed class PrivateKeyJwtAuthenticationTests(OpenIddictTestApplication app)
{
    [Fact(Skip = "Requires OpenIddict 7.5+ for client assertion type detection")]
    public async Task Should_issue_token_with_valid_private_key_jwt_assertion()
    {
        OidcTestClient client = app.CreateOidcClient();

        HttpResponseMessage response = await client.RawClientCredentialsWithAssertionAsync(
            OpenIddictTestApplication.TestPkjwtClientId,
            OpenIddictTestApplication.TestPkjwtPrivateKeyJwk);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var doc = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("access_token").GetString().ShouldNotBeNullOrEmpty();
        doc.RootElement.GetProperty("token_type").GetString().ShouldBe("Bearer");
    }

    [Fact]
    public async Task Should_reject_assertion_signed_with_wrong_key()
    {
        // Generate a completely different EC P-256 key pair
        using var wrongKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        ECParameters wrongParams = wrongKey.ExportParameters(includePrivateParameters: true);

        string wrongPrivateKeyJwk = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["kty"] = "EC",
            ["crv"] = "P-256",
            ["x"] = Base64UrlEncode(wrongParams.Q.X!),
            ["y"] = Base64UrlEncode(wrongParams.Q.Y!),
            ["d"] = Base64UrlEncode(wrongParams.D!),
        });

        OidcTestClient client = app.CreateOidcClient();

        HttpResponseMessage response = await client.RawClientCredentialsWithAssertionAsync(
            OpenIddictTestApplication.TestPkjwtClientId,
            wrongPrivateKeyJwk);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Should_reject_expired_assertion()
    {
        // Build an assertion that expired 5 minutes ago
        string expiredAssertion = OidcTestClient.BuildClientAssertionJwt(
            OpenIddictTestApplication.TestPkjwtClientId,
            OpenIddictTestApplication.TestPkjwtPrivateKeyJwk,
            DateTimeOffset.UtcNow.AddMinutes(-5));

        HttpClient httpClient = app.CreateHttpClient();

        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = OpenIddictTestApplication.TestPkjwtClientId,
            ["client_assertion"] = expiredAssertion,
            ["client_assertion_type"] = "urn:ietf:params:oauth:client-assertion-type:jwt-bearer",
            ["scope"] = "openid",
        });

        HttpResponseMessage response = await httpClient.PostAsync(
            "/connect/token", content, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Should_still_accept_client_secret_for_existing_clients()
    {
        OidcTestClient client = app.CreateOidcClient();

        JsonDocument result = await client.ClientCredentialsAsync(
            OpenIddictTestApplication.TestClientId,
            OpenIddictTestApplication.TestClientSecret);

        result.RootElement.GetProperty("access_token").GetString().ShouldNotBeNullOrEmpty();
        result.RootElement.GetProperty("token_type").GetString().ShouldBe("Bearer");
    }

    private static string Base64UrlEncode(byte[] data) =>
        Convert.ToBase64String(data)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
}
