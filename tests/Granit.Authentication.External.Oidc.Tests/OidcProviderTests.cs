using Granit.Modularity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Authentication.External.Oidc.Tests;

public sealed class OidcProviderTests
{
    private static ServiceProvider BuildProvider()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:External:Providers:0:Type"] = "Oidc",
            ["Authentication:External:Providers:0:Authority"] = "https://login.example.com/",
            ["Authentication:External:Providers:0:ClientId"] = "cid",
            ["Authentication:External:Providers:0:ClientSecret"] = "sec",
            ["Authentication:External:Providers:0:Scopes:0"] = "openid",
            ["Authentication:External:Providers:0:Scopes:1"] = "profile",
        });
        builder.Services.AddSingleton<IConfiguration>(builder.Configuration);

        GranitAuthenticationExternalOidcModule module = new();
        ServiceConfigurationContext context = new(builder.Services, builder.Configuration, builder);
        module.ConfigureServices(context);

        return builder.Services.BuildServiceProvider();
    }

    [Fact]
    public void Module_InheritsFromGranitModule() =>
        typeof(GranitAuthenticationExternalOidcModule).BaseType.ShouldBe(typeof(GranitModule));

    [Fact]
    public async Task ConfigureServices_RegistersOidcScheme()
    {
        await using ServiceProvider sp = BuildProvider();

        AuthenticationScheme? scheme = await sp
            .GetRequiredService<IAuthenticationSchemeProvider>().GetSchemeAsync("Oidc");

        scheme.ShouldNotBeNull();
    }

    [Fact]
    public void ConfigureServices_EnablesAuthorizationCodeWithPkce()
    {
        using ServiceProvider sp = BuildProvider();

        OpenIdConnectOptions options = sp
            .GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>().Get("Oidc");

        options.Authority.ShouldBe("https://login.example.com/");
        options.ClientId.ShouldBe("cid");
        options.ClientSecret.ShouldBe("sec");
        options.SignInScheme.ShouldBe(IdentityConstants.ExternalScheme);
        options.ResponseType.ShouldBe("code");
        options.UsePkce.ShouldBeTrue();
        options.SaveTokens.ShouldBeTrue();
        options.Scope.ShouldBe(["openid", "profile"]);
    }
}
