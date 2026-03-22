using System.Text.Json;
using Granit.OpenIddict.Tests.Integration.Fixtures;
using Granit.OpenIddict.Tests.Integration.Helpers;
using Shouldly;
using Xunit;

namespace Granit.OpenIddict.Tests.Integration.Flows;

[Collection("openiddict-integration")]
public sealed class DiscoveryEndpointTests(OpenIddictTestApplication app)
{
    [Fact]
    public async Task Should_return_openid_configuration()
    {
        OidcTestClient client = app.CreateOidcClient();

        JsonDocument discovery = await client.GetDiscoveryDocumentAsync();

        discovery.RootElement.TryGetProperty("issuer", out _).ShouldBeTrue();
        discovery.RootElement.TryGetProperty("token_endpoint", out _).ShouldBeTrue();
        discovery.RootElement.TryGetProperty("authorization_endpoint", out _).ShouldBeTrue();
        discovery.RootElement.TryGetProperty("userinfo_endpoint", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Should_expose_introspection_endpoint()
    {
        OidcTestClient client = app.CreateOidcClient();

        JsonDocument discovery = await client.GetDiscoveryDocumentAsync();

        string? introspectionEndpoint = discovery.RootElement
            .GetProperty("introspection_endpoint").GetString();

        introspectionEndpoint.ShouldNotBeNullOrEmpty();
        introspectionEndpoint.ShouldEndWith("/connect/introspect");
    }

    [Fact]
    public async Task Should_expose_revocation_endpoint()
    {
        OidcTestClient client = app.CreateOidcClient();

        JsonDocument discovery = await client.GetDiscoveryDocumentAsync();

        string? revocationEndpoint = discovery.RootElement
            .GetProperty("revocation_endpoint").GetString();

        revocationEndpoint.ShouldNotBeNullOrEmpty();
        revocationEndpoint.ShouldEndWith("/connect/revoke");
    }

    [Fact]
    public async Task Should_expose_pushed_authorization_request_endpoint()
    {
        OidcTestClient client = app.CreateOidcClient();

        JsonDocument discovery = await client.GetDiscoveryDocumentAsync();

        string? parEndpoint = discovery.RootElement
            .GetProperty("pushed_authorization_request_endpoint").GetString();

        parEndpoint.ShouldNotBeNullOrEmpty();
        parEndpoint.ShouldEndWith("/connect/par");
    }

    [Fact]
    public async Task Should_advertise_supported_grant_types()
    {
        OidcTestClient client = app.CreateOidcClient();

        JsonDocument discovery = await client.GetDiscoveryDocumentAsync();

        JsonElement grantTypes = discovery.RootElement.GetProperty("grant_types_supported");
        List<string> grants = [];
        foreach (JsonElement element in grantTypes.EnumerateArray())
        {
            grants.Add(element.GetString()!);
        }

        grants.ShouldContain("client_credentials");
        grants.ShouldContain("authorization_code");
        grants.ShouldContain("refresh_token");
    }
}
