using Granit.Modularity;
using Granit.Notifications.MobilePush.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Notifications.MobilePush.Tests;

/// <summary>
/// The module warns at startup when the in-memory device token store is still active
/// outside Development — in-memory is per-pod and non-durable, so it silently drops
/// pushes in a multi-replica deployment.
/// </summary>
public sealed class MobilePushInMemoryStoreWarningTests
{
    private static ServiceProvider BuildProvider(string environmentName, IMobilePushTokenReader reader)
    {
        IHostEnvironment environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(environmentName);

        ServiceCollection services = new();
        services.AddFakeLogging();
        services.AddSingleton(environment);
        services.AddSingleton(reader);
        return services.BuildServiceProvider();
    }

    private static bool WarnedFor(ServiceProvider sp)
    {
        new GranitNotificationsMobilePushModule().OnApplicationInitialization(new ApplicationInitializationContext(sp));
        return sp.GetFakeLogCollector().GetSnapshot().Any(r => r.Level == LogLevel.Warning);
    }

    [Fact]
    public void Warns_WhenInMemoryStore_OutsideDevelopment()
    {
        using ServiceProvider sp = BuildProvider("Production", new InMemoryMobilePushTokenStore(Substitute.For<IMobilePushTokenHasher>()));

        WarnedFor(sp).ShouldBeTrue();
    }

    [Fact]
    public void Silent_InDevelopment_EvenWithInMemoryStore()
    {
        using ServiceProvider sp = BuildProvider("Development", new InMemoryMobilePushTokenStore(Substitute.For<IMobilePushTokenHasher>()));

        WarnedFor(sp).ShouldBeFalse();
    }

    [Fact]
    public void Silent_WhenStoreIsNotInMemory()
    {
        using ServiceProvider sp = BuildProvider("Production", Substitute.For<IMobilePushTokenReader>());

        WarnedFor(sp).ShouldBeFalse();
    }
}
