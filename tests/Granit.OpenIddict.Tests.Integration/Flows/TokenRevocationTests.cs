using System.Net;
using System.Text.Json;
using Granit.OpenIddict.Tests.Integration.Fixtures;
using Granit.OpenIddict.Tests.Integration.Helpers;
using Shouldly;
using Xunit;

namespace Granit.OpenIddict.Tests.Integration.Flows;

[Collection("openiddict-integration")]
public sealed class TokenRevocationTests(OpenIddictTestApplication app)
{
    [Fact]
    public async Task Should_accept_revocation_of_valid_token()
    {
        OidcTestClient client = app.CreateOidcClient();

        JsonDocument tokenResult = await client.ClientCredentialsAsync(
            OpenIddictTestApplication.TestClientId,
            OpenIddictTestApplication.TestClientSecret);

        string accessToken = tokenResult.RootElement.GetProperty("access_token").GetString()!;

        HttpResponseMessage revokeResponse = await client.RevokeAsync(
            accessToken,
            OpenIddictTestApplication.TestClientId,
            OpenIddictTestApplication.TestClientSecret);

        revokeResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Should_accept_revocation_of_unknown_token_without_error()
    {
        OidcTestClient client = app.CreateOidcClient();

        HttpResponseMessage response = await client.RevokeAsync(
            "completely-bogus-token-value",
            OpenIddictTestApplication.TestClientId,
            OpenIddictTestApplication.TestClientSecret);

        // Per RFC 7009, the server MUST respond with 200 even if the token is invalid
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Should_reject_revocation_with_invalid_client_credentials()
    {
        OidcTestClient client = app.CreateOidcClient();

        JsonDocument tokenResult = await client.ClientCredentialsAsync(
            OpenIddictTestApplication.TestClientId,
            OpenIddictTestApplication.TestClientSecret);

        string accessToken = tokenResult.RootElement.GetProperty("access_token").GetString()!;

        HttpResponseMessage revokeResponse = await client.RevokeAsync(
            accessToken,
            OpenIddictTestApplication.TestClientId,
            "wrong-secret");

        revokeResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
