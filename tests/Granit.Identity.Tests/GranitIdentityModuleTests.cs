using Granit.Identity.Internal;
using Granit.Modularity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Granit.Identity.Tests;

public sealed class GranitIdentityModuleTests
{
    [Fact]
    public void ConfigureServices_RegistersNullIdentityProvider()
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        var context = new ServiceConfigurationContext(builder.Services, builder.Configuration, builder);
        var module = new GranitIdentityModule();

        module.ConfigureServices(context);

        ServiceProvider provider = builder.Services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();
        IIdentityProvider identityProvider = scope.ServiceProvider
            .GetRequiredService<IIdentityProvider>();

        identityProvider.ShouldBeOfType<NullIdentityProvider>();
    }

    [Fact]
    public void Module_InheritsFromGranitModule()
    {
        var module = new GranitIdentityModule();

        module.ShouldBeAssignableTo<GranitModule>();
    }
}
