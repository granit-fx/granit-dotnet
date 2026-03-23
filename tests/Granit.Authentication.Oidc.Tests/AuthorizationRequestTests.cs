using Granit.Authentication.Oidc.Requests;
using Shouldly;
using Xunit;

namespace Granit.Authentication.Oidc.Tests;

public sealed class AuthorizationRequestTests
{
    [Fact]
    public void ToUrl_BuildsCorrectUrl()
    {
        var request = new AuthorizationRequest
        {
            AuthorizationEndpoint = "https://idp.example.com/authorize",
            ClientId = "my-client",
            RedirectUri = "https://app.example.com/callback",
            ResponseType = "code",
            Scope = "openid profile",
            State = "abc123",
        };

        string url = request.ToUrl();

        url.ShouldStartWith("https://idp.example.com/authorize?");
        url.ShouldContain("client_id=my-client");
        url.ShouldContain("redirect_uri=https%3A%2F%2Fapp.example.com%2Fcallback");
        url.ShouldContain("response_type=code");
        url.ShouldContain("scope=openid%20profile");
        url.ShouldContain("state=abc123");
    }

    [Fact]
    public void ToUrl_IncludesPkceParameters()
    {
        var request = new AuthorizationRequest
        {
            AuthorizationEndpoint = "https://idp.example.com/authorize",
            ClientId = "my-client",
            RedirectUri = "https://app.example.com/callback",
            ResponseType = "code",
            Scope = "openid",
            State = "state-1",
            CodeChallenge = "challenge-value",
            CodeChallengeMethod = "S256",
        };

        string url = request.ToUrl();

        url.ShouldContain("code_challenge=challenge-value");
        url.ShouldContain("code_challenge_method=S256");
    }

    [Fact]
    public void ToUrl_IncludesNonceWhenProvided()
    {
        var request = new AuthorizationRequest
        {
            AuthorizationEndpoint = "https://idp.example.com/authorize",
            ClientId = "my-client",
            RedirectUri = "https://app.example.com/callback",
            ResponseType = "code",
            Scope = "openid",
            State = "state-1",
            Nonce = "nonce-value-42",
        };

        string url = request.ToUrl();

        url.ShouldContain("nonce=nonce-value-42");
    }

    [Fact]
    public void ToUrl_EscapesSpecialCharacters()
    {
        var request = new AuthorizationRequest
        {
            AuthorizationEndpoint = "https://idp.example.com/authorize",
            ClientId = "client with spaces",
            RedirectUri = "https://app.example.com/callback?param=value",
            ResponseType = "code",
            Scope = "openid",
            State = "a&b=c",
        };

        string url = request.ToUrl();

        url.ShouldContain("client_id=client%20with%20spaces");
        url.ShouldContain("state=a%26b%3Dc");
    }
}
