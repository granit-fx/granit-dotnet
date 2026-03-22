using System.Net;
using System.Text.Json;
using Granit.OpenIddict.Tests.Integration.Fixtures;
using Granit.OpenIddict.Tests.Integration.Helpers;
using Shouldly;
using Xunit;

namespace Granit.OpenIddict.Tests.Integration.Flows;

[Collection("openiddict-integration")]
public sealed class ClientCredentialsFlowTests(OpenIddictTestApplication app)
{
    [Fact]
    public async Task Should_issue_access_token_for_valid_client()
    {
        OidcTestClient client = app.CreateOidcClient();

        JsonDocument result = await client.ClientCredentialsAsync(
            OpenIddictTestApplication.TestClientId,
            OpenIddictTestApplication.TestClientSecret);

        result.RootElement.GetProperty("access_token").GetString().ShouldNotBeNullOrEmpty();
        result.RootElement.GetProperty("token_type").GetString().ShouldBe("Bearer");
    }

    [Fact]
    public async Task Should_include_expires_in_claim()
    {
        OidcTestClient client = app.CreateOidcClient();

        JsonDocument result = await client.ClientCredentialsAsync(
            OpenIddictTestApplication.TestClientId,
            OpenIddictTestApplication.TestClientSecret);

        int expiresIn = result.RootElement.GetProperty("expires_in").GetInt32();
        expiresIn.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task Should_reject_invalid_client_secret()
    {
        OidcTestClient client = app.CreateOidcClient();

        HttpResponseMessage response = await client.RawClientCredentialsAsync(
            OpenIddictTestApplication.TestClientId,
            "wrong-secret");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Should_reject_unknown_client_id()
    {
        OidcTestClient client = app.CreateOidcClient();

        HttpResponseMessage response = await client.RawClientCredentialsAsync(
            "nonexistent-client",
            "irrelevant-secret");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Should_reject_empty_grant_type()
    {
        HttpClient httpClient = app.CreateHttpClient();

        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = OpenIddictTestApplication.TestClientId,
            ["client_secret"] = OpenIddictTestApplication.TestClientSecret,
        });

        HttpResponseMessage response = await httpClient.PostAsync(
            "/connect/token", content, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
