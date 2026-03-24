using Granit.Oidc.Internal;
using Granit.Oidc.Requests;
using Shouldly;
using Xunit;

namespace Granit.Oidc.Tests;

public sealed class TokenRequestEncoderTests
{
    [Fact]
    public void Encode_AuthorizationCodeRequest_IncludesAllFields()
    {
        var request = new AuthorizationCodeTokenRequest
        {
            ClientId = "my-client",
            Code = "auth-code-123",
            RedirectUri = "https://app.example.com/callback",
            CodeVerifier = "verifier-xyz",
        };

        Dictionary<string, string> parameters = TokenRequestEncoder.ToParameters(request);

        parameters[OidcConstants.Parameters.ClientId].ShouldBe("my-client");
        parameters[OidcConstants.Parameters.GrantType].ShouldBe(OidcConstants.GrantTypes.AuthorizationCode);
        parameters[OidcConstants.Parameters.Code].ShouldBe("auth-code-123");
        parameters[OidcConstants.Parameters.RedirectUri].ShouldBe("https://app.example.com/callback");
        parameters[OidcConstants.Parameters.CodeVerifier].ShouldBe("verifier-xyz");
    }

    [Fact]
    public void Encode_RefreshTokenRequest_IncludesGrantType()
    {
        var request = new RefreshTokenRequest
        {
            ClientId = "my-client",
            RefreshToken = "refresh-token-abc",
            Scope = "openid",
        };

        Dictionary<string, string> parameters = TokenRequestEncoder.ToParameters(request);

        parameters[OidcConstants.Parameters.GrantType].ShouldBe(OidcConstants.GrantTypes.RefreshToken);
        parameters[OidcConstants.Parameters.RefreshToken].ShouldBe("refresh-token-abc");
        parameters[OidcConstants.Parameters.Scope].ShouldBe("openid");
    }

    [Fact]
    public void Encode_ClientCredentialsRequest_IncludesScope()
    {
        var request = new ClientCredentialsTokenRequest
        {
            ClientId = "service-client",
            Scope = "api.read api.write",
        };

        Dictionary<string, string> parameters = TokenRequestEncoder.ToParameters(request);

        parameters[OidcConstants.Parameters.GrantType].ShouldBe(OidcConstants.GrantTypes.ClientCredentials);
        parameters[OidcConstants.Parameters.Scope].ShouldBe("api.read api.write");
        parameters[OidcConstants.Parameters.ClientId].ShouldBe("service-client");
    }

    [Fact]
    public void ToParameters_ReturnsModifiableDictionary()
    {
        var request = new ClientCredentialsTokenRequest
        {
            ClientId = "my-client",
        };

        Dictionary<string, string> parameters = TokenRequestEncoder.ToParameters(request);

        // Verify the dictionary is mutable — callers need this for client auth strategies
        parameters["custom_param"] = "custom_value";
        parameters.ShouldContainKey("custom_param");
    }

    [Fact]
    public void ToParameters_IncludesAdditionalParameters()
    {
        var request = new ClientCredentialsTokenRequest
        {
            ClientId = "my-client",
            AdditionalParameters = new Dictionary<string, string>
            {
                ["audience"] = "https://api.example.com",
            },
        };

        Dictionary<string, string> parameters = TokenRequestEncoder.ToParameters(request);

        parameters["audience"].ShouldBe("https://api.example.com");
    }

    [Fact]
    public void ToParameters_AdditionalParameters_DoNotOverrideStandard()
    {
        var request = new ClientCredentialsTokenRequest
        {
            ClientId = "my-client",
            AdditionalParameters = new Dictionary<string, string>
            {
                [OidcConstants.Parameters.ClientId] = "should-not-override",
            },
        };

        Dictionary<string, string> parameters = TokenRequestEncoder.ToParameters(request);

        parameters[OidcConstants.Parameters.ClientId].ShouldBe("my-client");
    }
}
