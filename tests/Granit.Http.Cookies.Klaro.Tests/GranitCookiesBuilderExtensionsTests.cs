using Granit.Http.Cookies.Klaro.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.Http.Cookies.Klaro.Tests;

public sealed class GranitCookiesBuilderExtensionsTests
{
    [Fact]
    public void UseKlaro_RegistersConsentResolverDescriptor()
    {
        ServiceCollection services = new();
        GranitCookiesBuilder builder = new(services);

        builder.UseKlaro();

        services.ShouldContain(d =>
            d.ServiceType == typeof(IConsentResolver) &&
            d.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void UseKlaro_ReturnsSameBuilder()
    {
        ServiceCollection services = new();
        GranitCookiesBuilder builder = new(services);

        GranitCookiesBuilder result = builder.UseKlaro();

        result.ShouldBeSameAs(builder);
    }
}
