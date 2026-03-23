using Granit.Authentication.Oidc.Requests;
using Shouldly;
using Xunit;

namespace Granit.Authentication.Oidc.Tests;

public sealed class EndSessionRequestTests
{
    [Fact]
    public void ToUrl_BuildsCorrectUrl()
    {
        var request = new EndSessionRequest
        {
            EndSessionEndpoint = "https://idp.example.com/logout",
            IdTokenHint = "eyJhbGciOiJSUzI1NiJ9.test-id-token",
            PostLogoutRedirectUri = "https://app.example.com/signed-out",
            State = "logout-state-123",
        };

        string url = request.ToUrl();

        url.ShouldStartWith("https://idp.example.com/logout?");
        url.ShouldContain("id_token_hint=eyJhbGciOiJSUzI1NiJ9.test-id-token");
        url.ShouldContain("post_logout_redirect_uri=https%3A%2F%2Fapp.example.com%2Fsigned-out");
        url.ShouldContain("state=logout-state-123");
    }

    [Fact]
    public void ToUrl_IncludesClientIdWhenProvided()
    {
        var request = new EndSessionRequest
        {
            EndSessionEndpoint = "https://idp.example.com/logout",
            ClientId = "my-client",
        };

        string url = request.ToUrl();

        url.ShouldContain("client_id=my-client");
    }

    [Fact]
    public void ToUrl_OmitsNullFields()
    {
        var request = new EndSessionRequest
        {
            EndSessionEndpoint = "https://idp.example.com/logout",
            IdTokenHint = "test-hint",
        };

        string url = request.ToUrl();

        url.ShouldContain("id_token_hint=test-hint");
        url.ShouldNotContain("post_logout_redirect_uri");
        url.ShouldNotContain("client_id");
        url.ShouldNotContain("state");
    }
}
