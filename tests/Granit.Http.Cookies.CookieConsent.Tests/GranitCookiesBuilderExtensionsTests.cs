using Granit.Http.Cookies.CookieConsent.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.Http.Cookies.CookieConsent.Tests;

public sealed class GranitCookiesBuilderExtensionsTests
{
    [Fact]
    public void UseCookieConsent_RegistersConsentResolverDescriptor()
    {
        ServiceCollection services = new();
        GranitCookiesBuilder builder = new(services);

        builder.UseCookieConsent();

        services.ShouldContain(d =>
            d.ServiceType == typeof(IConsentResolver) &&
            d.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void UseCookieConsent_ReturnsSameBuilder()
    {
        ServiceCollection services = new();
        GranitCookiesBuilder builder = new(services);

        GranitCookiesBuilder result = builder.UseCookieConsent();

        result.ShouldBeSameAs(builder);
    }
}
