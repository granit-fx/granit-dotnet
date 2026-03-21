using Granit.Core.Modularity;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.Core.Tests.Modularity;

public sealed class ApplicationInitializationContextTests
{
    [Fact]
    public void Constructor_ExposesServiceProvider()
    {
        ServiceProvider provider = new ServiceCollection().BuildServiceProvider();
        ApplicationInitializationContext context = new(provider);

        context.ServiceProvider.ShouldBeSameAs(provider);
    }

    [Fact]
    public void ServiceProvider_CanResolveServices()
    {
        ServiceCollection services = new();
        services.AddSingleton<string>("test-value");
        ServiceProvider provider = services.BuildServiceProvider();
        ApplicationInitializationContext context = new(provider);

        string? resolved = context.ServiceProvider.GetService<string>();

        resolved.ShouldBe("test-value");
    }
}
