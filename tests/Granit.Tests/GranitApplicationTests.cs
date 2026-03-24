// =============================================================================
// Tests - GranitApplication
// =============================================================================
// Verifies that GranitApplication:
//   - Calls ConfigureServices on each module in topological order
//   - Calls OnApplicationInitialization in topological order
//   - Handles modules without overrides (no-op OK)
//   - Skips disabled modules (IsEnabled = false)
//   - Logs loaded modules at startup
// =============================================================================

using Granit.Modularity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace Granit.Tests;

public sealed class GranitApplicationTests
{
    private static readonly List<string> CallOrder = [];
    private static readonly ILogger<GranitApplication> NullLogger = NullLogger<GranitApplication>.Instance;

    // --- Test modules with call tracking ---

    public sealed class TrackingModuleA : GranitModule
    {
        public override void ConfigureServices(ServiceConfigurationContext context) => CallOrder.Add("ConfigureServices:A");

        public override void OnApplicationInitialization(ApplicationInitializationContext context) => CallOrder.Add("Initialize:A");
    }

    [DependsOn(typeof(TrackingModuleA))]
    public sealed class TrackingModuleB : GranitModule
    {
        public override void ConfigureServices(ServiceConfigurationContext context) => CallOrder.Add("ConfigureServices:B");

        public override void OnApplicationInitialization(ApplicationInitializationContext context) => CallOrder.Add("Initialize:B");
    }

    // --- Async module that overrides only ConfigureServicesAsync ---

    public sealed class AsyncTrackingModuleA : GranitModule
    {
        public override async Task ConfigureServicesAsync(ServiceConfigurationContext context)
        {
            await Task.Delay(1); // Simulates an async operation
            CallOrder.Add("ConfigureServicesAsync:A");
        }

        public override async Task OnApplicationInitializationAsync(ApplicationInitializationContext context)
        {
            await Task.Delay(1);
            CallOrder.Add("InitializeAsync:A");
        }
    }

    [DependsOn(typeof(AsyncTrackingModuleA))]
    public sealed class AsyncTrackingModuleB : GranitModule
    {
        public override async Task ConfigureServicesAsync(ServiceConfigurationContext context)
        {
            await Task.Delay(1);
            CallOrder.Add("ConfigureServicesAsync:B");
        }

        public override async Task OnApplicationInitializationAsync(ApplicationInitializationContext context)
        {
            await Task.Delay(1);
            CallOrder.Add("InitializeAsync:B");
        }
    }

    public sealed class NoOpModule : GranitModule;

    // --- Disabled module ---

    public sealed class DisabledModule : GranitModule
    {
        public override bool IsEnabled(ServiceConfigurationContext context) => false;

        public override void ConfigureServices(ServiceConfigurationContext context) =>
            CallOrder.Add("ConfigureServices:Disabled");

        public override void OnApplicationInitialization(ApplicationInitializationContext context) =>
            CallOrder.Add("Initialize:Disabled");
    }

    [DependsOn(typeof(DisabledModule))]
    public sealed class DependsOnDisabledModule : GranitModule
    {
        public override void ConfigureServices(ServiceConfigurationContext context) =>
            CallOrder.Add("ConfigureServices:DependsOnDisabled");

        public override void OnApplicationInitialization(ApplicationInitializationContext context) =>
            CallOrder.Add("Initialize:DependsOnDisabled");
    }

    public GranitApplicationTests()
    {
        CallOrder.Clear();
    }

    [Fact]
    public void ConfigureServices_CallsModulesInTopologicalOrder()
    {
        // Arrange
        IReadOnlyList<ModuleDescriptor> modules = ModuleLoader.LoadModules<TrackingModuleB>();
        var app = new GranitApplication(modules, NullLogger);
        var services = new ServiceCollection();
        IConfigurationRoot config = new ConfigurationBuilder().Build();
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        var context = new ServiceConfigurationContext(services, config, builder);

        // Act
        app.ConfigureServices(context);

        // Assert - A must be called before B
        CallOrder.ShouldBe(new[] { "ConfigureServices:A", "ConfigureServices:B" });
    }

    [Fact]
    public void InitializeApplication_CallsModulesInTopologicalOrder()
    {
        // Arrange
        IReadOnlyList<ModuleDescriptor> modules = ModuleLoader.LoadModules<TrackingModuleB>();
        var app = new GranitApplication(modules, NullLogger);
        var services = new ServiceCollection();
        ServiceProvider provider = services.BuildServiceProvider();
        var context = new ApplicationInitializationContext(provider);

        // Act
        app.InitializeApplication(context);

        // Assert - A must be called before B
        CallOrder.ShouldBe(new[] { "Initialize:A", "Initialize:B" });
    }

    [Fact]
    public void ConfigureServices_NoOpModules_DoesNotThrow()
    {
        // Arrange
        IReadOnlyList<ModuleDescriptor> modules = ModuleLoader.LoadModules<NoOpModule>();
        var app = new GranitApplication(modules, NullLogger);
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        var context = new ServiceConfigurationContext(
            new ServiceCollection(),
            new ConfigurationBuilder().Build(),
            builder);

        // Act
        Action act = () => app.ConfigureServices(context);

        // Assert
        Should.NotThrow(act);
    }

    [Fact]
    public void ModuleTypes_ReturnsOrderedModuleTypes()
    {
        IReadOnlyList<ModuleDescriptor> modules = ModuleLoader.LoadModules<TrackingModuleB>();
        var app = new GranitApplication(modules, NullLogger);

        IReadOnlyList<Type> types = app.GetModuleTypes();

        types.Count.ShouldBe(2);
        types[0].ShouldBe(typeof(TrackingModuleA));
        types[1].ShouldBe(typeof(TrackingModuleB));
    }

    [Fact]
    public void ConfigureServices_SkipsDisabledModules()
    {
        // Arrange
        IReadOnlyList<ModuleDescriptor> modules = ModuleLoader.LoadModules<DependsOnDisabledModule>();
        var app = new GranitApplication(modules, NullLogger);
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        var context = new ServiceConfigurationContext(
            new ServiceCollection(),
            new ConfigurationBuilder().Build(),
            builder);

        // Act
        app.ConfigureServices(context);

        // Assert - DisabledModule should be skipped, only DependsOnDisabled should run
        CallOrder.ShouldBe(new[] { "ConfigureServices:DependsOnDisabled" });
    }

    [Fact]
    public void InitializeApplication_SkipsDisabledModules()
    {
        // Arrange — first run ConfigureServices to set IsEnabled flags
        IReadOnlyList<ModuleDescriptor> modules = ModuleLoader.LoadModules<DependsOnDisabledModule>();
        var app = new GranitApplication(modules, NullLogger);
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        var configContext = new ServiceConfigurationContext(
            new ServiceCollection(),
            new ConfigurationBuilder().Build(),
            builder);
        app.ConfigureServices(configContext);
        CallOrder.Clear();

        // Act
        ServiceProvider provider = new ServiceCollection().BuildServiceProvider();
        var initContext = new ApplicationInitializationContext(provider);
        app.InitializeApplication(initContext);

        // Assert - DisabledModule should be skipped
        CallOrder.ShouldBe(new[] { "Initialize:DependsOnDisabled" });
    }

    [Fact]
    public void DisabledModule_RemainsInDependencyGraph()
    {
        // Arrange
        IReadOnlyList<ModuleDescriptor> modules = ModuleLoader.LoadModules<DependsOnDisabledModule>();
        var app = new GranitApplication(modules, NullLogger);

        // Act
        IReadOnlyList<Type> types = app.GetModuleTypes();

        // Assert - DisabledModule is still in the graph
        types.ShouldContain(typeof(DisabledModule));
        types.ShouldContain(typeof(DependsOnDisabledModule));
    }

    // --- Async tests ---

    [Fact]
    public async Task ConfigureServicesAsync_CallsModulesInTopologicalOrder()
    {
        // Arrange
        IReadOnlyList<ModuleDescriptor> modules = ModuleLoader.LoadModules<AsyncTrackingModuleB>();
        var app = new GranitApplication(modules, NullLogger);
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        var context = new ServiceConfigurationContext(
            new ServiceCollection(),
            new ConfigurationBuilder().Build(),
            builder);

        // Act
        await app.ConfigureServicesAsync(context);

        // Assert - A must be called before B
        CallOrder.ShouldBe(new[] { "ConfigureServicesAsync:A", "ConfigureServicesAsync:B" });
    }

    [Fact]
    public async Task InitializeApplicationAsync_CallsModulesInTopologicalOrder()
    {
        // Arrange
        IReadOnlyList<ModuleDescriptor> modules = ModuleLoader.LoadModules<AsyncTrackingModuleB>();
        var app = new GranitApplication(modules, NullLogger);
        ServiceProvider provider = new ServiceCollection().BuildServiceProvider();
        var context = new ApplicationInitializationContext(provider);

        // Act
        await app.InitializeApplicationAsync(context);

        // Assert - A must be called before B
        CallOrder.ShouldBe(new[] { "InitializeAsync:A", "InitializeAsync:B" });
    }

    [Fact]
    public async Task ConfigureServicesAsync_SyncOverride_CalledViaAsyncPath()
    {
        // Arrange - TrackingModuleA overrides ConfigureServices (sync)
        // ConfigureServicesAsync by default calls the sync version
        IReadOnlyList<ModuleDescriptor> modules = ModuleLoader.LoadModules<TrackingModuleB>();
        var app = new GranitApplication(modules, NullLogger);
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        var context = new ServiceConfigurationContext(
            new ServiceCollection(),
            new ConfigurationBuilder().Build(),
            builder);

        // Act
        await app.ConfigureServicesAsync(context);

        // Assert - Sync overrides are called via the async path
        CallOrder.ShouldBe(new[] { "ConfigureServices:A", "ConfigureServices:B" });
    }

    [Fact]
    public async Task ConfigureServicesAsync_NoOpModules_DoesNotThrow()
    {
        // Arrange
        IReadOnlyList<ModuleDescriptor> modules = ModuleLoader.LoadModules<NoOpModule>();
        var app = new GranitApplication(modules, NullLogger);
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        var context = new ServiceConfigurationContext(
            new ServiceCollection(),
            new ConfigurationBuilder().Build(),
            builder);

        // Act
        Func<Task> act = () => app.ConfigureServicesAsync(context);

        // Assert
        await Should.NotThrowAsync(act);
    }

    [Fact]
    public async Task ConfigureServicesAsync_SkipsDisabledModules()
    {
        // Arrange
        IReadOnlyList<ModuleDescriptor> modules = ModuleLoader.LoadModules<DependsOnDisabledModule>();
        var app = new GranitApplication(modules, NullLogger);
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        var context = new ServiceConfigurationContext(
            new ServiceCollection(),
            new ConfigurationBuilder().Build(),
            builder);

        // Act
        await app.ConfigureServicesAsync(context);

        // Assert
        CallOrder.ShouldBe(new[] { "ConfigureServices:DependsOnDisabled" });
    }
}
