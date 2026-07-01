using Granit.Modularity;
using Granit.Notifications.Abstractions;
using Granit.Notifications.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Notifications.Tests;

/// <summary>
/// The module warns at startup when the in-memory notification stores are still active
/// outside Development — in-memory state is per-pod and non-durable, so it is silently
/// lost and unshared in a multi-replica deployment.
/// </summary>
public sealed class NotificationsInMemoryStoreWarningTests
{
    private static ServiceProvider BuildProvider(string environmentName, IUserNotificationReader reader)
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
        new GranitNotificationsModule().OnApplicationInitialization(new ApplicationInitializationContext(sp));
        return sp.GetFakeLogCollector().GetSnapshot().Any(r => r.Level == LogLevel.Warning);
    }

    [Fact]
    public void Warns_WhenInMemoryStore_OutsideDevelopment()
    {
        using ServiceProvider sp = BuildProvider("Production", new InMemoryUserNotificationStore());

        WarnedFor(sp).ShouldBeTrue();
    }

    [Fact]
    public void Silent_InDevelopment_EvenWithInMemoryStore()
    {
        using ServiceProvider sp = BuildProvider("Development", new InMemoryUserNotificationStore());

        WarnedFor(sp).ShouldBeFalse();
    }

    [Fact]
    public void Silent_WhenStoreIsNotInMemory()
    {
        using ServiceProvider sp = BuildProvider("Production", Substitute.For<IUserNotificationReader>());

        WarnedFor(sp).ShouldBeFalse();
    }
}
