using Shouldly;
using Xunit;

namespace Granit.Authentication.Oidc.Tests;

public sealed class OidcConstantsTests
{
    [Fact]
    public void GrantTypes_AuthorizationCode_MatchesRfcValue() =>
        OidcConstants.GrantTypes.AuthorizationCode.ShouldBe("authorization_code");

    [Fact]
    public void GrantTypes_ClientCredentials_MatchesRfcValue() =>
        OidcConstants.GrantTypes.ClientCredentials.ShouldBe("client_credentials");

    [Fact]
    public void GrantTypes_RefreshToken_MatchesRfcValue() =>
        OidcConstants.GrantTypes.RefreshToken.ShouldBe("refresh_token");

    [Fact]
    public void GrantTypes_TokenExchange_MatchesRfc8693Value() =>
        OidcConstants.GrantTypes.TokenExchange.ShouldBe("urn:ietf:params:oauth:grant-type:token-exchange");

    [Fact]
    public void GrantTypes_DeviceCode_MatchesRfc8628Value() =>
        OidcConstants.GrantTypes.DeviceCode.ShouldBe("urn:ietf:params:oauth:grant-type:device_code");

    [Fact]
    public void ClientAssertionTypes_JwtBearer_MatchesRfc7523Value() =>
        OidcConstants.ClientAssertionTypes.JwtBearer.ShouldBe("urn:ietf:params:oauth:client-assertion-type:jwt-bearer");

    [Fact]
    public void CodeChallengeMethods_S256_MatchesRfc7636Value() =>
        OidcConstants.CodeChallengeMethods.S256.ShouldBe("S256");

    [Fact]
    public void TokenTypes_AccessToken_MatchesRfcValue() =>
        OidcConstants.TokenTypes.AccessToken.ShouldBe("access_token");

    [Fact]
    public void TokenTypes_RefreshToken_MatchesRfcValue() =>
        OidcConstants.TokenTypes.RefreshToken.ShouldBe("refresh_token");

    [Fact]
    public void Algorithms_ES256_MatchesJwaValue() =>
        OidcConstants.Algorithms.ES256.ShouldBe("ES256");
}
