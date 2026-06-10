using AspNet.Security.OAuth.Apple;
using Granit.Modularity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Authentication.External.Apple.Tests;

public sealed class AppleProviderTests
{
    private static ServiceProvider BuildProvider()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:External:Providers:0:Type"] = "Apple",
            ["Authentication:External:Providers:0:ClientId"] = "com.example.service",
            ["Authentication:External:Providers:0:Scopes:0"] = "name",
            ["Authentication:External:Providers:0:Scopes:1"] = "email",
            ["Authentication:External:Providers:0:Properties:TeamId"] = "TEAMID",
            ["Authentication:External:Providers:0:Properties:KeyId"] = "KEYID",
            ["Authentication:External:Providers:0:Properties:PrivateKey"] =
                "-----BEGIN PRIVATE KEY-----\nDUMMY\n-----END PRIVATE KEY-----",
        });
        builder.Services.AddSingleton<IConfiguration>(builder.Configuration);

        GranitAuthenticationExternalAppleModule module = new();
        ServiceConfigurationContext context = new(builder.Services, builder.Configuration, builder);
        module.ConfigureServices(context);

        return builder.Services.BuildServiceProvider();
    }

    [Fact]
    public void Module_InheritsFromGranitModule() =>
        typeof(GranitAuthenticationExternalAppleModule).BaseType.ShouldBe(typeof(GranitModule));

    [Fact]
    public async Task ConfigureServices_RegistersAppleScheme()
    {
        await using ServiceProvider sp = BuildProvider();

        AuthenticationScheme? scheme = await sp
            .GetRequiredService<IAuthenticationSchemeProvider>().GetSchemeAsync("Apple");

        scheme.ShouldNotBeNull();
    }

    [Fact]
    public void ConfigureServices_MapsServicesIdTeamKeyAndScopes()
    {
        using ServiceProvider sp = BuildProvider();

        AppleAuthenticationOptions options = sp
            .GetRequiredService<IOptionsMonitor<AppleAuthenticationOptions>>().Get("Apple");

        options.ClientId.ShouldBe("com.example.service");
        options.TeamId.ShouldBe("TEAMID");
        options.KeyId.ShouldBe("KEYID");
        options.GenerateClientSecret.ShouldBeTrue();
        options.Scope.ShouldBe(["name", "email"]);
    }
}
