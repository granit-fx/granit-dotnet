using Granit.Oidc.ClientAuthentication;
using Granit.Oidc.TokenManagement.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Oidc.TokenManagement.Tests.Options;

public sealed class ClientCredentialsOptionsValidatorTests
{
    private static ClientCredentialsOptionsValidator CreateValidator(string environmentName)
    {
        IHostEnvironment env = Substitute.For<IHostEnvironment>();
        env.EnvironmentName.Returns(environmentName);
        return new ClientCredentialsOptionsValidator(env);
    }

    private static ClientCredentialsOptions ValidOptions() => new()
    {
        Authority = "https://idp.example.com",
        ClientId = "svc-client",
        ClientSecret = "s3cr3t",
        ClientAuthenticationMethod = ClientAuthenticationMethod.ClientSecretPost,
    };

    [Fact]
    public void Validate_ValidConfiguration_Succeeds()
    {
        ValidateOptionsResult result = CreateValidator(Environments.Production)
            .Validate("api", ValidOptions());

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Validate_MissingAuthority_Fails()
    {
        ClientCredentialsOptions options = ValidOptions();
        options.Authority = "";

        ValidateOptionsResult result = CreateValidator(Environments.Production).Validate("api", options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(options.Authority));
    }

    [Fact]
    public void Validate_NonHttpsAuthorityInProduction_Fails()
    {
        ClientCredentialsOptions options = ValidOptions();
        options.Authority = "http://idp.example.com";

        ValidateOptionsResult result = CreateValidator(Environments.Production).Validate("api", options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("https");
    }

    [Fact]
    public void Validate_NonHttpsAuthorityInDevelopment_Succeeds()
    {
        ClientCredentialsOptions options = ValidOptions();
        options.Authority = "http://localhost:8080";

        ValidateOptionsResult result = CreateValidator(Environments.Development).Validate("api", options);

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ClientSecretPostWithoutSecret_Fails()
    {
        ClientCredentialsOptions options = ValidOptions();
        options.ClientSecret = null;

        ValidateOptionsResult result = CreateValidator(Environments.Production).Validate("api", options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(options.ClientSecret));
    }

    [Fact]
    public void Validate_PrivateKeyJwtWithoutSigningKey_Fails()
    {
        ClientCredentialsOptions options = ValidOptions();
        options.ClientAuthenticationMethod = ClientAuthenticationMethod.PrivateKeyJwt;
        options.ClientSecret = null;
        options.ClientSigningKeyJwk = null;

        ValidateOptionsResult result = CreateValidator(Environments.Production).Validate("api", options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(options.ClientSigningKeyJwk));
    }

    [Fact]
    public void Validate_NegativeCacheMargin_Fails()
    {
        ClientCredentialsOptions options = ValidOptions();
        options.CacheMargin = TimeSpan.FromSeconds(-1);

        ValidateOptionsResult result = CreateValidator(Environments.Production).Validate("api", options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(options.CacheMargin));
    }
}
