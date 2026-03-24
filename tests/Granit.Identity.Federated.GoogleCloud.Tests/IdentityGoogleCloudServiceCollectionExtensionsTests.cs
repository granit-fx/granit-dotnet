using Granit.Events;
using Granit.Identity.Federated.GoogleCloud.Extensions;
using Granit.Identity.Federated.GoogleCloud.Internal;
using Granit.Identity.Federated.GoogleCloud.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Identity.Federated.GoogleCloud.Tests;

public sealed class IdentityGoogleCloudServiceCollectionExtensionsTests
{
    private static ServiceCollection CreateServices(Dictionary<string, string?>? config = null)
    {
        ServiceCollection services = new();
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(config ?? new Dictionary<string, string?>
            {
                ["Identity:GoogleCloud:ProjectId"] = "test-project",
            })
            .Build();
        services.AddSingleton(configuration);
        services.AddLogging();

        // Register identity abstractions that AddGranitIdentityGoogleCloud expects
        services.AddSingleton(NSubstitute.Substitute.For<IDistributedEventBus>());

        return services;
    }

    [Fact]
    public void AddGranitIdentityGoogleCloud_RegistersOptions()
    {
        ServiceCollection services = CreateServices();

        services.AddGranitIdentityGoogleCloud();
        ServiceProvider sp = services.BuildServiceProvider();

        IOptions<GoogleCloudIdentityOptions> opts = sp.GetRequiredService<IOptions<GoogleCloudIdentityOptions>>();
        opts.Value.ProjectId.ShouldBe("test-project");
    }

    [Fact]
    public void AddGranitIdentityGoogleCloud_RegistersIdentityProvider()
    {
        ServiceCollection services = CreateServices();

        services.AddGranitIdentityGoogleCloud();
        ServiceProvider sp = services.BuildServiceProvider();

        // The provider is registered via AddIdentityProvider<T> which registers IIdentityProvider
        ServiceDescriptor? descriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(IIdentityProvider));
        descriptor.ShouldNotBeNull();
    }

    [Fact]
    public void AddGranitIdentityGoogleCloud_RegistersCapabilities()
    {
        ServiceCollection services = CreateServices();

        services.AddGranitIdentityGoogleCloud();

        ServiceDescriptor? descriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(IIdentityProviderCapabilities) &&
                 d.ImplementationType == typeof(GoogleCloudIdentityProviderCapabilities));
        descriptor.ShouldNotBeNull();
    }

    [Fact]
    public void AddGranitIdentityGoogleCloud_ReturnsServiceCollection()
    {
        ServiceCollection services = CreateServices();

        IServiceCollection result = services.AddGranitIdentityGoogleCloud();

        result.ShouldBeSameAs(services);
    }
}
