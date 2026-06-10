using Granit.Authentication.External.Extensions;
using Granit.Authentication.External.Options;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.Authentication.External.Tests;

public sealed class ExternalAuthenticationTests
{
    private static IConfiguration Config(params (string Type, string ClientId)[] providers)
    {
        var dict = new Dictionary<string, string?>();
        for (int i = 0; i < providers.Length; i++)
        {
            dict[$"{ExternalAuthOptions.SectionName}:Providers:{i}:Type"] = providers[i].Type;
            dict[$"{ExternalAuthOptions.SectionName}:Providers:{i}:ClientId"] = providers[i].ClientId;
        }

        return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
    }

    [Fact]
    public void AddExternalProviderSchemes_RegistersForMatchingTypeOnly()
    {
        var services = new ServiceCollection();
        IConfiguration config = Config(("Google", "gid"), ("GitHub", "ghid"));
        var registered = new List<string>();

        services.AddExternalProviderSchemes(
            config, "Google", (_, provider) => registered.Add(provider.SchemeName));

        registered.ShouldBe(["Google"]);
    }

    [Fact]
    public void AddExternalProviderSchemes_NoMatchingType_IsNoOp()
    {
        var services = new ServiceCollection();
        IConfiguration config = Config(("Google", "gid"));
        var registered = new List<string>();

        services.AddExternalProviderSchemes(
            config, "Facebook", (_, provider) => registered.Add(provider.SchemeName));

        registered.ShouldBeEmpty();
    }

    [Fact]
    public void SchemeName_DefaultsToType_AndHonoursExplicitName()
    {
        new ExternalAuthProvider { Type = "Google" }.SchemeName.ShouldBe("Google");
        new ExternalAuthProvider { Type = "Oidc", Name = "corp" }.SchemeName.ShouldBe("corp");
    }

    [Fact]
    public void ApplyExternalProvider_MapsCredentialsScopesAndSignInScheme()
    {
        var options = new OAuthOptions();
        var provider = new ExternalAuthProvider
        {
            Type = "Google",
            ClientId = "cid",
            ClientSecret = "sec",
            Scopes = ["openid", "email"],
            CallbackPath = "/cb",
        };

        options.ApplyExternalProvider(provider);

        options.ClientId.ShouldBe("cid");
        options.ClientSecret.ShouldBe("sec");
        options.SignInScheme.ShouldBe(IdentityConstants.ExternalScheme);
        options.CallbackPath.ToString().ShouldBe("/cb");
        options.Scope.ShouldBe(["openid", "email"]);
    }

    [Fact]
    public void ExternalAuthOptions_HasSensibleDefaults()
    {
        var options = new ExternalAuthOptions();

        options.AutoRegisterExternalUsers.ShouldBeTrue();
        options.Providers.ShouldBeEmpty();
        ExternalAuthOptions.SectionName.ShouldBe("Authentication:External");
    }
}
