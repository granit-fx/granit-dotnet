using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.Http.Cookies.Tests;

public sealed class GranitCookiesBuilderTests
{
    [Fact]
    public void RegisterCookie_AddsToCookieDefinitions()
    {
        ServiceCollection services = new();
        GranitCookiesBuilder builder = new(services);

        CookieDefinition definition = new("test", CookieCategory.Analytics, 365, false, "Test");
        builder.RegisterCookie(definition);

        builder.CookieDefinitions.ShouldHaveSingleItem();
        builder.CookieDefinitions[0].ShouldBe(definition);
    }

    [Fact]
    public void RegisterCookie_NullDefinition_ThrowsArgumentNullException()
    {
        ServiceCollection services = new();
        GranitCookiesBuilder builder = new(services);

        Action act = () => builder.RegisterCookie(null!);

        Should.Throw<ArgumentNullException>(act);
    }

    [Fact]
    public void RegisterCookie_ReturnsSelf_ForChaining()
    {
        ServiceCollection services = new();
        GranitCookiesBuilder builder = new(services);

        CookieDefinition definition = new("test", CookieCategory.Analytics, 365, false, "Test");
        GranitCookiesBuilder result = builder.RegisterCookie(definition);

        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void UseConsentResolver_RegistersConsentResolverInServices()
    {
        ServiceCollection services = new();
        GranitCookiesBuilder builder = new(services);

        builder.UseConsentResolver<FakeConsentResolver>();

        ServiceProvider provider = services.BuildServiceProvider();
        IConsentResolver? resolver = provider.GetService<IConsentResolver>();
        resolver.ShouldNotBeNull();
        resolver.ShouldBeOfType<FakeConsentResolver>();
    }

    [Fact]
    public void UseConsentResolver_ReturnsSelf_ForChaining()
    {
        ServiceCollection services = new();
        GranitCookiesBuilder builder = new(services);

        GranitCookiesBuilder result = builder.UseConsentResolver<FakeConsentResolver>();

        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void MultipleRegistrations_AllCookiesAdded()
    {
        ServiceCollection services = new();
        GranitCookiesBuilder builder = new(services);

        builder
            .RegisterCookie(new("cookie1", CookieCategory.Analytics, 365, false, "C1"))
            .RegisterCookie(new("cookie2", CookieCategory.Marketing, 180, false, "C2"))
            .RegisterCookie(new("cookie3", CookieCategory.StrictlyNecessary, 1, true, "C3"));

        builder.CookieDefinitions.Count.ShouldBe(3);
    }

    private sealed class FakeConsentResolver : IConsentResolver
    {
        public Task<bool> ResolveAsync(Microsoft.AspNetCore.Http.HttpContext httpContext, CookieCategory category) =>
            Task.FromResult(true);
    }
}
