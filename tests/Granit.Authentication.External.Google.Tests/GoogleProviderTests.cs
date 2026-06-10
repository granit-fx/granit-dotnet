using Granit.Modularity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Authentication.External.Google.Tests;

public sealed class GoogleProviderTests
{
    private static ServiceProvider BuildProvider()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:External:Providers:0:Type"] = "Google",
            ["Authentication:External:Providers:0:ClientId"] = "cid",
            ["Authentication:External:Providers:0:ClientSecret"] = "sec",
            ["Authentication:External:Providers:0:Scopes:0"] = "openid",
            ["Authentication:External:Providers:0:Scopes:1"] = "email",
        });
        builder.Services.AddSingleton<IConfiguration>(builder.Configuration);

        GranitAuthenticationExternalGoogleModule module = new();
        ServiceConfigurationContext context = new(builder.Services, builder.Configuration, builder);
        module.ConfigureServices(context);

        return builder.Services.BuildServiceProvider();
    }

    [Fact]
    public void Module_InheritsFromGranitModule() =>
        typeof(GranitAuthenticationExternalGoogleModule).BaseType.ShouldBe(typeof(GranitModule));

    [Fact]
    public async Task ConfigureServices_RegistersGoogleScheme()
    {
        await using ServiceProvider sp = BuildProvider();

        AuthenticationScheme? scheme = await sp
            .GetRequiredService<IAuthenticationSchemeProvider>().GetSchemeAsync("Google");

        scheme.ShouldNotBeNull();
    }

    [Fact]
    public void ConfigureServices_MapsCredentialsScopesAndSignInScheme()
    {
        using ServiceProvider sp = BuildProvider();

        GoogleOptions options = sp
            .GetRequiredService<IOptionsMonitor<GoogleOptions>>().Get("Google");

        options.ClientId.ShouldBe("cid");
        options.ClientSecret.ShouldBe("sec");
        options.SignInScheme.ShouldBe(IdentityConstants.ExternalScheme);
        options.Scope.ShouldBe(["openid", "email"]);
    }
}
