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

    [Fact]
    public void AllProperties_CanBeSet()
    {
        var options = new ClientCredentialsOptions
        {
            Authority = "https://idp.example.com",
            ClientId = "my-client",
            ClientSecret = "s3cr3t",
            Scope = "api1 api2",
            ClientAuthenticationMethod = ClientAuthenticationMethod.PrivateKeyJwt,
            ClientSigningKeyJwk = """{"kty":"EC"}""",
            UseDPoP = true,
            CacheMargin = TimeSpan.FromSeconds(15),
        };

        options.Authority.ShouldBe("https://idp.example.com");
        options.ClientId.ShouldBe("my-client");
        options.ClientSecret.ShouldBe("s3cr3t");
        options.Scope.ShouldBe("api1 api2");
        options.ClientAuthenticationMethod.ShouldBe(ClientAuthenticationMethod.PrivateKeyJwt);
        options.ClientSigningKeyJwk.ShouldBe("""{"kty":"EC"}""");
        options.UseDPoP.ShouldBeTrue();
        options.CacheMargin.ShouldBe(TimeSpan.FromSeconds(15));
    }
}
