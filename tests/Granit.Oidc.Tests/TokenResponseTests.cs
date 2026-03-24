using System.Text.Json;
using Granit.Oidc.Responses;
using Shouldly;
using Xunit;

namespace Granit.Oidc.Tests;

public sealed class TokenResponseTests
{
    [Fact]
    public void FromJson_ParsesSuccessfulResponse()
    {
        var json = JsonDocument.Parse("""
            {
                "access_token": "eyJhbGciOiJSUzI1NiJ9.test",
                "refresh_token": "dGVzdC1yZWZyZXNoLXRva2Vu",
                "expires_in": 3600,
                "token_type": "Bearer",
                "scope": "openid profile"
            }
            """);

        var response = TokenResponse.FromJson(json.RootElement);

        response.IsSuccess.ShouldBeTrue();
        response.AccessToken.ShouldBe("eyJhbGciOiJSUzI1NiJ9.test");
        response.RefreshToken.ShouldBe("dGVzdC1yZWZyZXNoLXRva2Vu");
        response.ExpiresIn.ShouldBe(3600);
        response.TokenType.ShouldBe("Bearer");
        response.Scope.ShouldBe("openid profile");
    }

    [Fact]
    public void FromJson_ParsesErrorResponse()
    {
        var json = JsonDocument.Parse("""
            {
                "error": "invalid_grant",
                "error_description": "The authorization code has expired."
            }
            """);

        var response = TokenResponse.FromJson(json.RootElement);

        response.IsSuccess.ShouldBeFalse();
        response.Error.ShouldNotBeNull();
        response.Error.Error.ShouldBe("invalid_grant");
        response.Error.ErrorDescription.ShouldBe("The authorization code has expired.");
    }

    [Fact]
    public void FromError_CreatesErrorResponse()
    {
        var response = TokenResponse.FromError("server_error", "Something went wrong");

        response.IsSuccess.ShouldBeFalse();
        response.Error.ShouldNotBeNull();
        response.Error.Error.ShouldBe("server_error");
        response.Error.ErrorDescription.ShouldBe("Something went wrong");
    }

    [Fact]
    public void IsSuccess_TrueWhenAccessTokenPresent()
    {
        var response = new TokenResponse { AccessToken = "test-token" };

        response.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void IsSuccess_FalseWhenErrorPresent()
    {
        var response = new TokenResponse
        {
            Error = new OidcError("invalid_client"),
        };

        response.IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public void FromJson_PreservesDPoPNonce()
    {
        var json = JsonDocument.Parse("""
            {
                "access_token": "test-token",
                "expires_in": 300,
                "token_type": "DPoP"
            }
            """);

        var response = TokenResponse.FromJson(json.RootElement, dpopNonce: "server-nonce-123");

        response.DPoPNonce.ShouldBe("server-nonce-123");
    }
}
