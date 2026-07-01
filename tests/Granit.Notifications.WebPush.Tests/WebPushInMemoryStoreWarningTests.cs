using Granit.Modularity;
using Granit.Notifications.WebPush.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Notifications.WebPush.Tests;

/// <summary>
/// The module warns at startup when the in-memory subscription store is still active
/// outside Development — in-memory is per-pod and non-durable, so it silently drops
/// pushes in a multi-replica deployment.
/// </summary>
public sealed class WebPushInMemoryStoreWarningTests
{
    private static ServiceProvider BuildProvider(string environmentName, IWebPushSubscriptionReader reader)
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
        new GranitNotificationsWebPushModule().OnApplicationInitialization(new ApplicationInitializationContext(sp));
        return sp.GetFakeLogCollector().GetSnapshot().Any(r => r.Level == LogLevel.Warning);
    }

    [Fact]
    public void Warns_WhenInMemoryStore_OutsideDevelopment()
    {
        using ServiceProvider sp = BuildProvider("Production", new InMemoryWebPushSubscriptionStore());

        WarnedFor(sp).ShouldBeTrue();
    }

    [Fact]
    public void Silent_InDevelopment_EvenWithInMemoryStore()
    {
        using ServiceProvider sp = BuildProvider("Development", new InMemoryWebPushSubscriptionStore());

        WarnedFor(sp).ShouldBeFalse();
    }

    [Fact]
    public void Silent_WhenStoreIsNotInMemory()
    {
        using ServiceProvider sp = BuildProvider("Production", Substitute.For<IWebPushSubscriptionReader>());

        WarnedFor(sp).ShouldBeFalse();
    }
}
