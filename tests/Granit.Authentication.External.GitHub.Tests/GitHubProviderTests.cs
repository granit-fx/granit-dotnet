using AspNet.Security.OAuth.GitHub;
using Granit.Modularity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Authentication.External.GitHub.Tests;

public sealed class GitHubProviderTests
{
    private static ServiceProvider BuildProvider()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:External:Providers:0:Type"] = "GitHub",
            ["Authentication:External:Providers:0:ClientId"] = "cid",
            ["Authentication:External:Providers:0:ClientSecret"] = "sec",
            ["Authentication:External:Providers:0:Scopes:0"] = "read:user",
            ["Authentication:External:Providers:0:Scopes:1"] = "user:email",
        });
        builder.Services.AddSingleton<IConfiguration>(builder.Configuration);

        GranitAuthenticationExternalGitHubModule module = new();
        ServiceConfigurationContext context = new(builder.Services, builder.Configuration, builder);
        module.ConfigureServices(context);

        return builder.Services.BuildServiceProvider();
    }

    [Fact]
    public void Module_InheritsFromGranitModule() =>
        typeof(GranitAuthenticationExternalGitHubModule).BaseType.ShouldBe(typeof(GranitModule));

    [Fact]
    public async Task ConfigureServices_RegistersGitHubScheme()
    {
        await using ServiceProvider sp = BuildProvider();

        AuthenticationScheme? scheme = await sp
            .GetRequiredService<IAuthenticationSchemeProvider>().GetSchemeAsync("GitHub");

        scheme.ShouldNotBeNull();
    }

    [Fact]
    public void ConfigureServices_MapsCredentialsScopesAndSignInScheme()
    {
        using ServiceProvider sp = BuildProvider();

        GitHubAuthenticationOptions options = sp
            .GetRequiredService<IOptionsMonitor<GitHubAuthenticationOptions>>().Get("GitHub");

        options.ClientId.ShouldBe("cid");
        options.ClientSecret.ShouldBe("sec");
        options.SignInScheme.ShouldBe(IdentityConstants.ExternalScheme);
        options.Scope.ShouldBe(["read:user", "user:email"]);
    }
}
