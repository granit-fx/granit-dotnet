using Granit.Http.Cookies.CookieConsent.Extensions;
using Granit.Http.Cookies.CookieConsent.Internal;
using Granit.Http.Cookies.CookieConsent.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Http.Cookies.CookieConsent.Tests;

public sealed class CookieConsentServiceCollectionExtensionsTests
{
    private static IConfiguration CreateConfiguration(
        string cookieName = "cc_cookie",
        string analyticsCategoryName = "analytics")
    {
        Dictionary<string, string?> configData = new()
        {
            ["Http:Cookies:CookieConsent:CookieName"] = cookieName,
            ["Http:Cookies:CookieConsent:AnalyticsCategoryName"] = analyticsCategoryName,
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();
    }

    [Fact]
    public void AddGranitCookiesCookieConsent_RegistersConsentResolver()
    {
        ServiceCollection services = new();
        services.AddSingleton(CreateConfiguration());
        services.AddLogging();

        services.AddGranitCookiesCookieConsent();

        ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();
        IConsentResolver? resolver = scope.ServiceProvider.GetService<IConsentResolver>();
        resolver.ShouldNotBeNull();
        resolver.ShouldBeOfType<CookieConsentConsentResolver>();
    }

    [Fact]
    public void AddGranitCookiesCookieConsent_RegistersCookieDefinitionContributor()
    {
        ServiceCollection services = new();
        services.AddSingleton(CreateConfiguration());
        services.AddLogging();

        services.AddGranitCookiesCookieConsent();

        ServiceProvider provider = services.BuildServiceProvider();
        ICookieDefinitionContributor? contributor = provider.GetService<ICookieDefinitionContributor>();
        contributor.ShouldNotBeNull();
        contributor.ShouldBeOfType<CookieConsentCookieDefinitionContributor>();
    }

    [Fact]
    public void AddGranitCookiesCookieConsent_BindsOptions()
    {
        ServiceCollection services = new();
        services.AddSingleton(CreateConfiguration(cookieName: "my-consent"));
        services.AddLogging();

        services.AddGranitCookiesCookieConsent();

        ServiceProvider provider = services.BuildServiceProvider();
        IOptions<CookieConsentOptions> options = provider.GetRequiredService<IOptions<CookieConsentOptions>>();
        options.Value.CookieName.ShouldBe("my-consent");
    }

    [Fact]
    public void AddGranitCookiesCookieConsent_DefaultCookieName()
    {
        ServiceCollection services = new();
        IConfiguration emptyConfig = new ConfigurationBuilder().Build();
        services.AddSingleton(emptyConfig);
        services.AddLogging();

        services.AddGranitCookiesCookieConsent();

        ServiceProvider provider = services.BuildServiceProvider();
        IOptions<CookieConsentOptions> options = provider.GetRequiredService<IOptions<CookieConsentOptions>>();
        options.Value.CookieName.ShouldBe("cc_cookie");
    }
}
