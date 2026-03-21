using Granit.Core.Modularity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Granit.Core.Tests.Modularity;

public sealed class GranitModuleTests
{
    // -------------------------------------------------------------------------
    // IsEnabled — default returns true
    // -------------------------------------------------------------------------

    [Fact]
    public void IsEnabled_Default_ReturnsTrue()
    {
        TestModule module = new();
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        ServiceConfigurationContext context = new(
            new ServiceCollection(),
            new ConfigurationBuilder().Build(),
            builder);

        module.IsEnabled(context).ShouldBeTrue();
    }

    // -------------------------------------------------------------------------
    // ConfigureServices — default is no-op
    // -------------------------------------------------------------------------

    [Fact]
    public void ConfigureServices_Default_DoesNotThrow()
    {
        TestModule module = new();
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        ServiceConfigurationContext context = new(
            new ServiceCollection(),
            new ConfigurationBuilder().Build(),
            builder);

        Should.NotThrow(() => module.ConfigureServices(context));
    }

    // -------------------------------------------------------------------------
    // ConfigureServicesAsync — default calls sync version
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ConfigureServicesAsync_Default_CallsSyncVersion()
    {
        TrackingModule module = new();
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        ServiceConfigurationContext context = new(
            new ServiceCollection(),
            new ConfigurationBuilder().Build(),
            builder);

        await module.ConfigureServicesAsync(context);

        module.SyncConfigureCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task ConfigureServicesAsync_Default_ReturnsCompletedTask()
    {
        TestModule module = new();
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        ServiceConfigurationContext context = new(
            new ServiceCollection(),
            new ConfigurationBuilder().Build(),
            builder);

        Task task = module.ConfigureServicesAsync(context);

        task.IsCompleted.ShouldBeTrue();
        await task;
    }

    // -------------------------------------------------------------------------
    // OnApplicationInitialization — default is no-op
    // -------------------------------------------------------------------------

    [Fact]
    public void OnApplicationInitialization_Default_DoesNotThrow()
    {
        TestModule module = new();
        ServiceProvider provider = new ServiceCollection().BuildServiceProvider();
        ApplicationInitializationContext context = new(provider);

        Should.NotThrow(() => module.OnApplicationInitialization(context));
    }

    // -------------------------------------------------------------------------
    // OnApplicationInitializationAsync — default calls sync version
    // -------------------------------------------------------------------------

    [Fact]
    public async Task OnApplicationInitializationAsync_Default_CallsSyncVersion()
    {
        TrackingModule module = new();
        ServiceProvider provider = new ServiceCollection().BuildServiceProvider();
        ApplicationInitializationContext context = new(provider);

        await module.OnApplicationInitializationAsync(context);

        module.SyncInitCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task OnApplicationInitializationAsync_Default_ReturnsCompletedTask()
    {
        TestModule module = new();
        ServiceProvider provider = new ServiceCollection().BuildServiceProvider();
        ApplicationInitializationContext context = new(provider);

        Task task = module.OnApplicationInitializationAsync(context);

        task.IsCompleted.ShouldBeTrue();
        await task;
    }

    // -------------------------------------------------------------------------
    // Test fixtures
    // -------------------------------------------------------------------------

    private sealed class TestModule : GranitModule;

    private sealed class TrackingModule : GranitModule
    {
        public bool SyncConfigureCalled { get; private set; }
        public bool SyncInitCalled { get; private set; }

        public override void ConfigureServices(ServiceConfigurationContext context) =>
            SyncConfigureCalled = true;

        public override void OnApplicationInitialization(ApplicationInitializationContext context) =>
            SyncInitCalled = true;
    }
}
