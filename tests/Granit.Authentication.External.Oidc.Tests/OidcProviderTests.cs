using Granit.Authentication.External.Extensions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.Authentication.External.Oidc.Tests;

public sealed class OidcProviderTests
{
    [Fact]
    public async Task RegistersScheme_WhenProviderConfigured()
    {
        var services = new ServiceCollection();
        IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:External:Providers:0:Type"] = "Oidc",
            ["Authentication:External:Providers:0:Authority"] = "https://login.example.com/",
            ["Authentication:External:Providers:0:ClientId"] = "cid",
            ["Authentication:External:Providers:0:ClientSecret"] = "sec",
        }).Build();

        services.AddExternalProviderSchemes(
            config, "Oidc",
            (builder, provider) => builder.AddOpenIdConnect(provider.SchemeName, options =>
            {
                options.Authority = provider.Authority;
                options.ClientId = provider.ClientId;
                options.ClientSecret = provider.ClientSecret;
                options.SignInScheme = IdentityConstants.ExternalScheme;
            }));

        await using ServiceProvider sp = services.BuildServiceProvider();
        AuthenticationScheme? scheme = await sp
            .GetRequiredService<IAuthenticationSchemeProvider>().GetSchemeAsync("Oidc");

        scheme.ShouldNotBeNull();
    }
}
