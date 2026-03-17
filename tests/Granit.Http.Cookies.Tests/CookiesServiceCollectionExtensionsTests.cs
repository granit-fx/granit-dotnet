using Granit.Http.Cookies.Extensions;
using Granit.Timing.Extensions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Http.Cookies.Tests;

public sealed class CookiesServiceCollectionExtensionsTests
{
    [Fact]
    public void AddGranitCookies_RegistersServices()
    {
        ServiceCollection services = new();
        services.AddGranitTiming();
        services.AddGranitCookies(cookies =>
        {
            cookies.UseConsentResolver<FakeConsentResolver>();
        });

        ServiceProvider provider = services.BuildServiceProvider();

        provider.GetService<ICookieRegistry>().ShouldNotBeNull();
        provider.GetService<IGranitCookieManager>().ShouldNotBeNull();
        provider.GetService<IConsentResolver>().ShouldNotBeNull();
    }

    [Fact]
    public void AddGranitCookies_RegistersCookiesInRegistry()
    {
        ServiceCollection services = new();
        services.AddGranitCookies(cookies =>
        {
            cookies.RegisterCookie(new("session", CookieCategory.StrictlyNecessary, 1, true, "Session"));
            cookies.RegisterCookie(new("_ga", CookieCategory.Analytics, 730, false, "Google Analytics"));
        });

        ServiceProvider provider = services.BuildServiceProvider();
        ICookieRegistry? registry = provider.GetService<ICookieRegistry>();

        registry.ShouldNotBeNull();
        registry!.IsRegistered("session").ShouldBeTrue();
        registry.IsRegistered("_ga").ShouldBeTrue();
        registry.GetAll().Count.ShouldBe(2);
    }

    [Fact]
    public void AddGranitCookies_WithConsentResolver_RegistersResolver()
    {
        ServiceCollection services = new();
        services.AddGranitCookies(cookies =>
        {
            cookies.UseConsentResolver<FakeConsentResolver>();
        });

        ServiceProvider provider = services.BuildServiceProvider();
        IConsentResolver? resolver = provider.GetService<IConsentResolver>();

        resolver.ShouldNotBeNull();
        resolver.ShouldBeOfType<FakeConsentResolver>();
    }

    [Fact]
    public void AddGranitCookies_NullConfigure_ThrowsArgumentNullException()
    {
        ServiceCollection services = new();

        Action act = () => services.AddGranitCookies(null!);

        Should.Throw<ArgumentNullException>(act);
    }

    private sealed class FakeConsentResolver : IConsentResolver
    {
        public Task<bool> ResolveAsync(Microsoft.AspNetCore.Http.HttpContext httpContext, CookieCategory category) =>
            Task.FromResult(true);
    }
}
