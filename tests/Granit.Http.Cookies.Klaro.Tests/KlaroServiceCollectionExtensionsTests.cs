using Granit.Http.Cookies.Klaro.Extensions;
using Granit.Http.Cookies.Klaro.Internal;
using Granit.Http.Cookies.Klaro.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Http.Cookies.Klaro.Tests;

public sealed class KlaroServiceCollectionExtensionsTests
{
    private static IConfiguration CreateConfiguration(string cookieName = "klaro")
    {
        Dictionary<string, string?> configData = new()
        {
            ["Klaro:CookieName"] = cookieName,
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();
    }

    [Fact]
    public void AddGranitCookiesKlaro_RegistersConsentResolver()
    {
        ServiceCollection services = new();
        services.AddSingleton(CreateConfiguration());
        services.AddSingleton(Substitute.For<IThirdPartyServiceRegistry>());
        services.AddLogging();

        services.AddGranitCookiesKlaro();

        ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();
        IConsentResolver? resolver = scope.ServiceProvider.GetService<IConsentResolver>();
        resolver.ShouldNotBeNull();
        resolver.ShouldBeOfType<KlaroConsentResolver>();
    }

    [Fact]
    public void AddGranitCookiesKlaro_BindsOptions()
    {
        ServiceCollection services = new();
        services.AddSingleton(CreateConfiguration(cookieName: "my-consent"));
        services.AddSingleton(Substitute.For<IThirdPartyServiceRegistry>());
        services.AddLogging();

        services.AddGranitCookiesKlaro();

        ServiceProvider provider = services.BuildServiceProvider();
        IOptions<KlaroOptions> options = provider.GetRequiredService<IOptions<KlaroOptions>>();
        options.Value.CookieName.ShouldBe("my-consent");
    }

    [Fact]
    public void AddGranitCookiesKlaro_DefaultCookieName()
    {
        ServiceCollection services = new();
        services.AddSingleton(CreateConfiguration());
        services.AddSingleton(Substitute.For<IThirdPartyServiceRegistry>());
        services.AddLogging();

        services.AddGranitCookiesKlaro();

        ServiceProvider provider = services.BuildServiceProvider();
        IOptions<KlaroOptions> options = provider.GetRequiredService<IOptions<KlaroOptions>>();
        options.Value.CookieName.ShouldBe("klaro");
    }
}
