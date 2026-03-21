using Granit.Core.Modularity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace Granit.Core.Tests.Modularity;

public sealed class GranitApplicationAdditionalTests
{
    private static readonly ILogger<GranitApplication> NullLogger = NullLogger<GranitApplication>.Instance;

    // -------------------------------------------------------------------------
    // GetModuleInstances — returns only enabled modules
    // -------------------------------------------------------------------------

    [Fact]
    public void GetModuleInstances_ReturnsEnabledModulesOnly()
    {
        IReadOnlyList<ModuleDescriptor> modules = ModuleLoader.LoadModules<DependsOnDisabledModule>();
        GranitApplication app = new(modules, NullLogger);

        // First call ConfigureServices to set IsEnabled flags
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        ServiceConfigurationContext context = new(
            new ServiceCollection(),
            new ConfigurationBuilder().Build(),
            builder);
        app.ConfigureServices(context);

        IReadOnlyList<GranitModule> instances = app.GetModuleInstances();

        instances.Count.ShouldBe(1);
        instances[0].ShouldBeOfType<DependsOnDisabledModule>();
    }

    [Fact]
    public void GetModuleInstances_AllEnabled_ReturnsAll()
    {
        IReadOnlyList<ModuleDescriptor> modules = ModuleLoader.LoadModules<EnabledModuleB>();
        GranitApplication app = new(modules, NullLogger);

        // ConfigureServices to set IsEnabled flags
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        ServiceConfigurationContext context = new(
            new ServiceCollection(),
            new ConfigurationBuilder().Build(),
            builder);
        app.ConfigureServices(context);

        IReadOnlyList<GranitModule> instances = app.GetModuleInstances();

        instances.Count.ShouldBe(2);
    }

    // -------------------------------------------------------------------------
    // GetModuleTypes — includes all modules (enabled and disabled)
    // -------------------------------------------------------------------------

    [Fact]
    public void GetModuleTypes_IncludesDisabledModules()
    {
        IReadOnlyList<ModuleDescriptor> modules = ModuleLoader.LoadModules<DependsOnDisabledModule>();
        GranitApplication app = new(modules, NullLogger);

        IReadOnlyList<Type> types = app.GetModuleTypes();

        types.ShouldContain(typeof(DisabledModule));
        types.ShouldContain(typeof(DependsOnDisabledModule));
    }

    // -------------------------------------------------------------------------
    // InitializeApplicationAsync — skips disabled modules
    // -------------------------------------------------------------------------

    private static readonly List<string> InitOrder = [];

    [Fact]
    public async Task InitializeApplicationAsync_SkipsDisabledModules()
    {
        InitOrder.Clear();
        IReadOnlyList<ModuleDescriptor> modules = ModuleLoader.LoadModules<DependsOnDisabledModule>();
        GranitApplication app = new(modules, NullLogger);

        // First set IsEnabled flags via ConfigureServices
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        ServiceConfigurationContext context = new(
            new ServiceCollection(),
            new ConfigurationBuilder().Build(),
            builder);
        app.ConfigureServices(context);

        // Now initialize
        ServiceProvider provider = new ServiceCollection().BuildServiceProvider();
        ApplicationInitializationContext initContext = new(provider);
        await app.InitializeApplicationAsync(initContext);

        // DisabledModule should not have been initialized
        InitOrder.ShouldNotContain("Disabled");
    }

    // -------------------------------------------------------------------------
    // Test fixtures
    // -------------------------------------------------------------------------

    public sealed class DisabledModule : GranitModule
    {
        public override bool IsEnabled(ServiceConfigurationContext context) => false;

        public override void OnApplicationInitialization(ApplicationInitializationContext context) =>
            InitOrder.Add("Disabled");

        public override Task OnApplicationInitializationAsync(ApplicationInitializationContext context)
        {
            InitOrder.Add("Disabled");
            return Task.CompletedTask;
        }
    }

    [DependsOn(typeof(DisabledModule))]
    public sealed class DependsOnDisabledModule : GranitModule
    {
        public override void OnApplicationInitialization(ApplicationInitializationContext context) =>
            InitOrder.Add("DependsOnDisabled");

        public override Task OnApplicationInitializationAsync(ApplicationInitializationContext context)
        {
            InitOrder.Add("DependsOnDisabled");
            return Task.CompletedTask;
        }
    }

    public sealed class EnabledModuleA : GranitModule;

    [DependsOn(typeof(EnabledModuleA))]
    public sealed class EnabledModuleB : GranitModule;
}
