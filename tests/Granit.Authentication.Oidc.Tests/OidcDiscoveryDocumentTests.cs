using System.Text.Json;
using Granit.Authentication.Oidc.Discovery;
using Shouldly;
using Xunit;

namespace Granit.Authentication.Oidc.Tests;

public sealed class OidcDiscoveryDocumentTests
{
    [Fact]
    public void FromJson_ParsesMinimalDocument()
    {
        var json = JsonDocument.Parse("""
            {
                "issuer": "https://idp.example.com",
                "authorization_endpoint": "https://idp.example.com/authorize",
                "token_endpoint": "https://idp.example.com/token"
            }
            """);

        var doc = OidcDiscoveryDocument.FromJson(json.RootElement);

        doc.Issuer.ShouldBe("https://idp.example.com");
        doc.AuthorizationEndpoint.ShouldBe("https://idp.example.com/authorize");
        doc.TokenEndpoint.ShouldBe("https://idp.example.com/token");
    }

    [Fact]
    public void FromJson_ParsesOptionalFields()
    {
        var json = JsonDocument.Parse("""
            {
                "issuer": "https://idp.example.com",
                "authorization_endpoint": "https://idp.example.com/authorize",
                "token_endpoint": "https://idp.example.com/token",
                "revocation_endpoint": "https://idp.example.com/revoke",
                "end_session_endpoint": "https://idp.example.com/logout",
                "pushed_authorization_request_endpoint": "https://idp.example.com/par",
                "jwks_uri": "https://idp.example.com/.well-known/jwks.json",
                "userinfo_endpoint": "https://idp.example.com/userinfo"
            }
            """);

        var doc = OidcDiscoveryDocument.FromJson(json.RootElement);

        doc.RevocationEndpoint.ShouldBe("https://idp.example.com/revoke");
        doc.EndSessionEndpoint.ShouldBe("https://idp.example.com/logout");
        doc.PushedAuthorizationRequestEndpoint.ShouldBe("https://idp.example.com/par");
        doc.JwksUri.ShouldBe("https://idp.example.com/.well-known/jwks.json");
        doc.UserInfoEndpoint.ShouldBe("https://idp.example.com/userinfo");
    }

    [Fact]
    public void FromJson_ParsesArrayFields()
    {
        var json = JsonDocument.Parse("""
            {
                "issuer": "https://idp.example.com",
                "authorization_endpoint": "https://idp.example.com/authorize",
                "token_endpoint": "https://idp.example.com/token",
                "scopes_supported": ["openid", "profile", "email"],
                "grant_types_supported": ["authorization_code", "client_credentials", "refresh_token"],
                "response_types_supported": ["code"],
                "dpop_signing_alg_values_supported": ["ES256"]
            }
            """);

        var doc = OidcDiscoveryDocument.FromJson(json.RootElement);

        doc.ScopesSupported.ShouldBe(["openid", "profile", "email"]);
        doc.GrantTypesSupported.ShouldBe(["authorization_code", "client_credentials", "refresh_token"]);
        doc.ResponseTypesSupported.ShouldBe(["code"]);
        doc.DPoPSigningAlgValuesSupported.ShouldBe(["ES256"]);
    }

    [Fact]
    public void FromJson_OptionalFieldsDefaultToNullOrEmpty()
    {
        var json = JsonDocument.Parse("""
            {
                "issuer": "https://idp.example.com",
                "authorization_endpoint": "https://idp.example.com/authorize",
                "token_endpoint": "https://idp.example.com/token"
            }
            """);

        var doc = OidcDiscoveryDocument.FromJson(json.RootElement);

        doc.RevocationEndpoint.ShouldBeNull();
        doc.EndSessionEndpoint.ShouldBeNull();
        doc.PushedAuthorizationRequestEndpoint.ShouldBeNull();
        doc.JwksUri.ShouldBeNull();
        doc.ScopesSupported.ShouldBeEmpty();
        doc.GrantTypesSupported.ShouldBeEmpty();
        doc.ResponseTypesSupported.ShouldBeEmpty();
        doc.DPoPSigningAlgValuesSupported.ShouldBeEmpty();
    }
}
