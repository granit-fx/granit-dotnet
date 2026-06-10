using Granit.Authentication.External.Extensions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.Authentication.External.Microsoft.Tests;

public sealed class MicrosoftProviderTests
{
    [Fact]
    public async Task RegistersScheme_WhenProviderConfigured()
    {
        var services = new ServiceCollection();
        IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:External:Providers:0:Type"] = "Microsoft",
            ["Authentication:External:Providers:0:ClientId"] = "cid",
            ["Authentication:External:Providers:0:ClientSecret"] = "sec",
        }).Build();

        services.AddExternalProviderSchemes(
            config, "Microsoft",
            (builder, provider) => builder.AddMicrosoftAccount(
                provider.SchemeName, options => options.ApplyExternalProvider(provider)));

        await using ServiceProvider sp = services.BuildServiceProvider();
        AuthenticationScheme? scheme = await sp
            .GetRequiredService<IAuthenticationSchemeProvider>().GetSchemeAsync("Microsoft");

        scheme.ShouldNotBeNull();
    }
}
