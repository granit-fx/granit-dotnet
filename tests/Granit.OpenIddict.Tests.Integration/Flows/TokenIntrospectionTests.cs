using System.Net;
using System.Text.Json;
using Granit.OpenIddict.Tests.Integration.Fixtures;
using Granit.OpenIddict.Tests.Integration.Helpers;
using Shouldly;
using Xunit;

namespace Granit.OpenIddict.Tests.Integration.Flows;

[Collection("openiddict-integration")]
public sealed class TokenIntrospectionTests(OpenIddictTestApplication app)
{
    [Fact]
    public async Task Should_introspect_active_token()
    {
        OidcTestClient client = app.CreateOidcClient();

        JsonDocument tokenResult = await client.ClientCredentialsAsync(
            OpenIddictTestApplication.TestClientId,
            OpenIddictTestApplication.TestClientSecret);

        string accessToken = tokenResult.RootElement.GetProperty("access_token").GetString()!;

        JsonDocument introspection = await client.IntrospectAsync(
            accessToken,
            OpenIddictTestApplication.TestClientId,
            OpenIddictTestApplication.TestClientSecret);

        introspection.RootElement.GetProperty("active").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task Should_return_inactive_for_bogus_token()
    {
        OidcTestClient client = app.CreateOidcClient();

        JsonDocument introspection = await client.IntrospectAsync(
            "this-is-not-a-real-token",
            OpenIddictTestApplication.TestClientId,
            OpenIddictTestApplication.TestClientSecret);

        introspection.RootElement.GetProperty("active").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task Should_reject_introspection_with_invalid_client()
    {
        OidcTestClient client = app.CreateOidcClient();

        HttpResponseMessage response = await client.RawIntrospectAsync(
            "any-token",
            OpenIddictTestApplication.TestClientId,
            "wrong-secret");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
