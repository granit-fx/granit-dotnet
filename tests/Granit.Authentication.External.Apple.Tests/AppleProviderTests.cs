using Granit.Authentication.External.Extensions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.Authentication.External.Apple.Tests;

public sealed class AppleProviderTests
{
    [Fact]
    public async Task RegistersScheme_WhenProviderConfigured()
    {
        var services = new ServiceCollection();
        IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:External:Providers:0:Type"] = "Apple",
            ["Authentication:External:Providers:0:ClientId"] = "com.example.service",
            ["Authentication:External:Providers:0:Properties:TeamId"] = "TEAMID",
            ["Authentication:External:Providers:0:Properties:KeyId"] = "KEYID",
        }).Build();

        services.AddExternalProviderSchemes(
            config, "Apple",
            (builder, provider) => builder.AddApple(provider.SchemeName, options =>
            {
                options.ClientId = provider.ClientId;
                options.SignInScheme = IdentityConstants.ExternalScheme;
            }));

        await using ServiceProvider sp = services.BuildServiceProvider();
        AuthenticationScheme? scheme = await sp
            .GetRequiredService<IAuthenticationSchemeProvider>().GetSchemeAsync("Apple");

        scheme.ShouldNotBeNull();
    }
}
