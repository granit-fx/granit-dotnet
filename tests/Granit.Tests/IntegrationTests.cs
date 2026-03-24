// =============================================================================
// Tests - Integration AddGranit<T> / UseGranit
// =============================================================================
// Verifie le pipeline complet :
//   - AddGranit<T>() enregistre GranitApplication en singleton
//   - Les services enregistres par les modules sont resolus
//   - UseGranit() appelle OnApplicationInitialization
// =============================================================================

using Granit.Extensions;
using Granit.Modularity;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.Tests;

public sealed class IntegrationTests
{
    // --- Modules de test avec services ---

    public interface ITestService;
    private sealed class TestServiceImpl : ITestService;

    public sealed class TestLeafModule : GranitModule
    {
        public override void ConfigureServices(ServiceConfigurationContext context) => context.Services.AddSingleton<ITestService, TestServiceImpl>();
    }

    private static bool _initializationCalled;
    private static bool _asyncInitializationCalled;

    [DependsOn(typeof(TestLeafModule))]
    public sealed class TestRootModule : GranitModule
    {
        public override void OnApplicationInitialization(ApplicationInitializationContext context) => _initializationCalled = true;
    }

    // --- Modules async ---

    public sealed class AsyncTestLeafModule : GranitModule
    {
        public override async Task ConfigureServicesAsync(ServiceConfigurationContext context)
        {
            await Task.Delay(1);
            context.Services.AddSingleton<ITestService, TestServiceImpl>();
        }
    }

    [DependsOn(typeof(AsyncTestLeafModule))]
    public sealed class AsyncTestRootModule : GranitModule
    {
        public override async Task OnApplicationInitializationAsync(ApplicationInitializationContext context)
        {
            await Task.Delay(1);
            _asyncInitializationCalled = true;
        }
    }

    public IntegrationTests()
    {
        _initializationCalled = false;
        _asyncInitializationCalled = false;
    }

    [Fact]
    public void AddGranit_RegistersGranitApplicationAsSingleton()
    {
        // Arrange
        WebApplicationBuilder builder = WebApplication.CreateBuilder();

        // Act
        builder.AddGranit<TestRootModule>();
        using WebApplication app = builder.Build();

        // Assert
        GranitApplication? granitApp = app.Services.GetService<GranitApplication>();
        granitApp.ShouldNotBeNull();

        GranitApplication? secondResolve = app.Services.GetService<GranitApplication>();
        secondResolve.ShouldBeSameAs(granitApp);
    }

    [Fact]
    public void AddGranit_ModuleServicesAreRegistered()
    {
        // Arrange
        WebApplicationBuilder builder = WebApplication.CreateBuilder();

        // Act
        builder.AddGranit<TestRootModule>();
        using WebApplication app = builder.Build();

        // Assert - TestLeafModule enregistre ITestService
        ITestService? service = app.Services.GetService<ITestService>();
        service.ShouldNotBeNull();
    }

    [Fact]
    public void UseGranit_CallsOnApplicationInitialization()
    {
        // Arrange
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.AddGranit<TestRootModule>();
        using WebApplication app = builder.Build();

        // Act
        app.UseGranit();

        // Assert
        _initializationCalled.ShouldBeTrue();
    }

    [Fact]
    public void AddGranit_ModuleTypesAreInTopologicalOrder()
    {
        // Arrange
        WebApplicationBuilder builder = WebApplication.CreateBuilder();

        // Act
        builder.AddGranit<TestRootModule>();
        using WebApplication app = builder.Build();

        // Assert
        GranitApplication granitApp = app.Services.GetRequiredService<GranitApplication>();
        granitApp.GetModuleTypes().ShouldBe(new[] { typeof(TestLeafModule),
            typeof(TestRootModule) });
    }

    // --- Tests async ---

    [Fact]
    public async Task AddGranitAsync_RegistersGranitApplicationAsSingleton()
    {
        // Arrange
        WebApplicationBuilder builder = WebApplication.CreateBuilder();

        // Act
        await builder.AddGranitAsync<AsyncTestRootModule>();
        await using WebApplication app = builder.Build();

        // Assert
        GranitApplication? granitApp = app.Services.GetService<GranitApplication>();
        granitApp.ShouldNotBeNull();

        GranitApplication? secondResolve = app.Services.GetService<GranitApplication>();
        secondResolve.ShouldBeSameAs(granitApp);
    }

    [Fact]
    public async Task AddGranitAsync_ModuleServicesAreRegistered()
    {
        // Arrange
        WebApplicationBuilder builder = WebApplication.CreateBuilder();

        // Act
        await builder.AddGranitAsync<AsyncTestRootModule>();
        await using WebApplication app = builder.Build();

        // Assert - AsyncTestLeafModule enregistre ITestService via ConfigureServicesAsync
        ITestService? service = app.Services.GetService<ITestService>();
        service.ShouldNotBeNull();
    }

    [Fact]
    public async Task UseGranitAsync_CallsOnApplicationInitializationAsync()
    {
        // Arrange
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        await builder.AddGranitAsync<AsyncTestRootModule>();
        await using WebApplication app = builder.Build();

        // Act
        await app.UseGranitAsync();

        // Assert
        _asyncInitializationCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task AddGranitAsync_ModuleTypesAreInTopologicalOrder()
    {
        // Arrange
        WebApplicationBuilder builder = WebApplication.CreateBuilder();

        // Act
        await builder.AddGranitAsync<AsyncTestRootModule>();
        await using WebApplication app = builder.Build();

        // Assert
        GranitApplication granitApp = app.Services.GetRequiredService<GranitApplication>();
        granitApp.GetModuleTypes().ShouldBe(new[] { typeof(AsyncTestLeafModule),
            typeof(AsyncTestRootModule) });
    }

    // --- Tests fluent builder API ---

    [Fact]
    public void AddGranit_FluentBuilder_RegistersModules()
    {
        // Arrange
        WebApplicationBuilder builder = WebApplication.CreateBuilder();

        // Act
        builder.AddGranit(granit => granit
            .AddModule<TestRootModule>());
        using WebApplication app = builder.Build();

        // Assert — both TestLeafModule (via [DependsOn]) and TestRootModule should be loaded
        GranitApplication granitApp = app.Services.GetRequiredService<GranitApplication>();
        granitApp.GetModuleTypes().ShouldContain(typeof(TestLeafModule));
        granitApp.GetModuleTypes().ShouldContain(typeof(TestRootModule));
    }

    [Fact]
    public void AddGranit_FluentBuilder_ModuleServicesAreRegistered()
    {
        // Arrange
        WebApplicationBuilder builder = WebApplication.CreateBuilder();

        // Act
        builder.AddGranit(granit => granit.AddModule<TestRootModule>());
        using WebApplication app = builder.Build();

        // Assert
        ITestService? service = app.Services.GetService<ITestService>();
        service.ShouldNotBeNull();
    }

    [Fact]
    public async Task AddGranitAsync_FluentBuilder_RegistersModules()
    {
        // Arrange
        WebApplicationBuilder builder = WebApplication.CreateBuilder();

        // Act
        await builder.AddGranitAsync(granit => granit
            .AddModule<AsyncTestRootModule>());
        await using WebApplication app = builder.Build();

        // Assert
        GranitApplication granitApp = app.Services.GetRequiredService<GranitApplication>();
        granitApp.GetModuleTypes().ShouldContain(typeof(AsyncTestLeafModule));
        granitApp.GetModuleTypes().ShouldContain(typeof(AsyncTestRootModule));
    }

    [Fact]
    public void AddGranit_FluentBuilder_ThrowsOnNullConfigure()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        Should.Throw<ArgumentNullException>(() =>
            builder.AddGranit((Action<GranitBuilder>)null!));
    }
}
