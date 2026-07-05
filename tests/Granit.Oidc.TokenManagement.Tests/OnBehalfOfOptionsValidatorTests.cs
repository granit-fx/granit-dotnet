using Granit.Oidc.ClientAuthentication;
using Granit.Oidc.TokenManagement.Options;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Oidc.TokenManagement.Tests;

public sealed class OnBehalfOfOptionsValidatorTests
{
    private readonly IHostEnvironment _environment = Substitute.For<IHostEnvironment>();

    private OnBehalfOfOptionsValidator CreateValidator(string environmentName = "Production")
    {
        _environment.EnvironmentName = environmentName;
        return new OnBehalfOfOptionsValidator(_environment);
    }

    private static OnBehalfOfOptions ValidOptions() => new()
    {
        Authority = "https://auth.example.com",
        ClientId = "client-id",
        ClientSecret = "secret",
        Audience = "https://api.example.com",
        AllowedHosts = ["api.example.com"],
        ClientAuthenticationMethod = ClientAuthenticationMethod.ClientSecretPost,
    };

    [Fact]
    public void Validate_ValidOptions_Succeeds() =>
        CreateValidator().Validate(null, ValidOptions()).Succeeded.ShouldBeTrue();

    [Fact]
    public void Validate_MissingAuthority_Fails()
    {
        OnBehalfOfOptions options = ValidOptions();
        options.Authority = "";

        CreateValidator().Validate("client", options).Failed.ShouldBeTrue();
    }

    [Fact]
    public void Validate_NonHttpsAuthorityInProduction_Fails()
    {
        OnBehalfOfOptions options = ValidOptions();
        options.Authority = "http://auth.example.com";

        ValidateOptionsResultAssert(CreateValidator("Production").Validate(null, options), "must be https");
    }

    [Fact]
    public void Validate_NonHttpsAuthorityInDevelopment_Succeeds()
    {
        OnBehalfOfOptions options = ValidOptions();
        options.Authority = "http://localhost:5000";

        CreateValidator("Development").Validate(null, options).Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Validate_MissingClientId_Fails()
    {
        OnBehalfOfOptions options = ValidOptions();
        options.ClientId = "";

        CreateValidator().Validate(null, options).Failed.ShouldBeTrue();
    }

    [Fact]
    public void Validate_MissingAudience_Fails()
    {
        OnBehalfOfOptions options = ValidOptions();
        options.Audience = "";

        ValidateOptionsResultAssert(CreateValidator().Validate(null, options), "Audience");
    }

    [Fact]
    public void Validate_EmptyAllowedHosts_Fails()
    {
        OnBehalfOfOptions options = ValidOptions();
        options.AllowedHosts = [];

        ValidateOptionsResultAssert(CreateValidator().Validate(null, options), "AllowedHosts");
    }

    [Fact]
    public void Validate_ClientSecretPostWithoutSecret_Fails()
    {
        OnBehalfOfOptions options = ValidOptions();
        options.ClientSecret = null;
        options.ClientAuthenticationMethod = ClientAuthenticationMethod.ClientSecretPost;

        ValidateOptionsResultAssert(CreateValidator().Validate(null, options), "ClientSecret");
    }

    [Fact]
    public void Validate_PrivateKeyJwtWithoutSigningKey_Fails()
    {
        OnBehalfOfOptions options = ValidOptions();
        options.ClientSecret = null;
        options.ClientSigningKeyJwk = null;
        options.ClientAuthenticationMethod = ClientAuthenticationMethod.PrivateKeyJwt;

        ValidateOptionsResultAssert(CreateValidator().Validate(null, options), "ClientSigningKeyJwk");
    }

    [Fact]
    public void Validate_PrivateKeyJwtWithSigningKey_Succeeds()
    {
        OnBehalfOfOptions options = ValidOptions();
        options.ClientSecret = null;
        options.ClientSigningKeyJwk = "signing-key-jwk";
        options.ClientAuthenticationMethod = ClientAuthenticationMethod.PrivateKeyJwt;

        CreateValidator().Validate(null, options).Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Validate_NegativeSafetyMargin_Fails()
    {
        OnBehalfOfOptions options = ValidOptions();
        options.TokenLifetimeSafetyMargin = TimeSpan.FromSeconds(-1);

        ValidateOptionsResultAssert(CreateValidator().Validate(null, options), "TokenLifetimeSafetyMargin");
    }

    private static void ValidateOptionsResultAssert(
        Microsoft.Extensions.Options.ValidateOptionsResult result,
        string expectedFragment)
    {
        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(expectedFragment);
    }
}
