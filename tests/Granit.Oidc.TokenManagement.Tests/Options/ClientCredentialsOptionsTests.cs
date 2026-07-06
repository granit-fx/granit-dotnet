using Granit.Oidc.ClientAuthentication;
using Granit.Oidc.TokenManagement.Options;
using Shouldly;
using Xunit;

namespace Granit.Oidc.TokenManagement.Tests.Options;

public sealed class ClientCredentialsOptionsTests
{
    [Fact]
    public void Authority_DefaultsToEmpty() =>
        new ClientCredentialsOptions().Authority.ShouldBe(string.Empty);

    [Fact]
    public void ClientId_DefaultsToEmpty() =>
        new ClientCredentialsOptions().ClientId.ShouldBe(string.Empty);

    [Fact]
    public void ClientSecret_DefaultsToNull() =>
        new ClientCredentialsOptions().ClientSecret.ShouldBeNull();

    [Fact]
    public void Scope_DefaultsToNull() =>
        new ClientCredentialsOptions().Scope.ShouldBeNull();

    [Fact]
    public void ClientAuthenticationMethod_DefaultsToClientSecretPost() =>
        new ClientCredentialsOptions().ClientAuthenticationMethod
            .ShouldBe(ClientAuthenticationMethod.ClientSecretPost);

    [Fact]
    public void ClientSigningKeyJwk_DefaultsToNull() =>
        new ClientCredentialsOptions().ClientSigningKeyJwk.ShouldBeNull();

    [Fact]
    public void UseDPoP_DefaultsToFalse() =>
        new ClientCredentialsOptions().UseDPoP.ShouldBeFalse();

    [Fact]
    public void CacheMargin_DefaultsToNull() =>
        new ClientCredentialsOptions().CacheMargin.ShouldBeNull();
}
