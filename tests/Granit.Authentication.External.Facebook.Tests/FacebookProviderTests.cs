using Granit.Authentication.External.Extensions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.Authentication.External.Facebook.Tests;

public sealed class FacebookProviderTests
{
    [Fact]
    public async Task RegistersScheme_WhenProviderConfigured()
    {
        var services = new ServiceCollection();
        IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:External:Providers:0:Type"] = "Facebook",
            ["Authentication:External:Providers:0:ClientId"] = "cid",
            ["Authentication:External:Providers:0:ClientSecret"] = "sec",
        }).Build();

        services.AddExternalProviderSchemes(
            config, "Facebook",
            (builder, provider) => builder.AddFacebook(
                provider.SchemeName, options => options.ApplyExternalProvider(provider)));

        await using ServiceProvider sp = services.BuildServiceProvider();
        AuthenticationScheme? scheme = await sp
            .GetRequiredService<IAuthenticationSchemeProvider>().GetSchemeAsync("Facebook");

        scheme.ShouldNotBeNull();
    }
}
