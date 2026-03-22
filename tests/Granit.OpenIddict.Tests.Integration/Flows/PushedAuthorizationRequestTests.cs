using System.Net;
using System.Text.Json;
using Granit.OpenIddict.Tests.Integration.Fixtures;
using Granit.OpenIddict.Tests.Integration.Helpers;
using Shouldly;
using Xunit;

namespace Granit.OpenIddict.Tests.Integration.Flows;

[Collection("openiddict-integration")]
public sealed class PushedAuthorizationRequestTests(OpenIddictTestApplication app)
{
    [Fact]
    public async Task Should_accept_par_and_return_request_uri()
    {
        OidcTestClient client = app.CreateOidcClient();

        HttpResponseMessage response = await client.RawPushedAuthorizationRequestAsync(
            OpenIddictTestApplication.TestClientId,
            OpenIddictTestApplication.TestClientSecret,
            "http://localhost/callback");

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var doc = JsonDocument.Parse(json);

        string? requestUri = doc.RootElement.GetProperty("request_uri").GetString();
        requestUri.ShouldNotBeNullOrEmpty("PAR response must include a request_uri");

        doc.RootElement.TryGetProperty("expires_in", out JsonElement expiresIn).ShouldBeTrue();
        expiresIn.GetInt32().ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task Should_reject_par_without_client_authentication()
    {
        OidcTestClient client = app.CreateOidcClient();
        HttpClient httpClient = app.CreateHttpClient();

        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["response_type"] = "code",
            ["redirect_uri"] = "http://localhost/callback",
            ["scope"] = "openid",
            ["code_challenge"] = "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM",
            ["code_challenge_method"] = "S256",
        });

        HttpResponseMessage response = await httpClient.PostAsync(
            "/connect/par", content, TestContext.Current.CancellationToken);

        // OpenIddict returns 400 for missing client authentication on the PAR endpoint
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Should_reject_par_with_invalid_client()
    {
        OidcTestClient client = app.CreateOidcClient();

        HttpResponseMessage response = await client.RawPushedAuthorizationRequestAsync(
            "nonexistent-client",
            "wrong-secret",
            "http://localhost/callback");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
