// =============================================================================
// Tests - ApiVersioningServiceCollectionExtensions
// =============================================================================
// Vérifie que AddGranitApiVersioning enregistre correctement les services
// Asp.Versioning et applique la configuration appsettings.
// =============================================================================

using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using Granit.Http.ApiVersioning.Extensions;
using Granit.Http.ApiVersioning.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Http.ApiVersioning.Tests;

public sealed class ApiVersioningServiceCollectionExtensionsTests
{
    [Fact]
    public void AddGranitApiVersioning_RegistersApiVersioningServices()
    {
        // Arrange
        ServiceCollection services = new();
        IConfiguration configuration = new ConfigurationBuilder().Build();
        services.AddSingleton<IConfiguration>(configuration);

        // Act
        services.AddGranitApiVersioning();

        // Assert — IApiVersionReader, IApiVersionSelector, etc. should be registered
        ServiceDescriptor? descriptor = services.FirstOrDefault(
            d => d.ServiceType.Name.Contains("ApiVersion", StringComparison.OrdinalIgnoreCase));
        descriptor.ShouldNotBeNull("AddApiVersioning must register versioning services");
    }

    [Fact]
    public void AddGranitApiVersioning_WithCustomOptions_AppliesConfiguration()
    {
        // Arrange
        ServiceCollection services = new();
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Http:ApiVersioning:DefaultMajorVersion"] = "2",
                ["Http:ApiVersioning:ReportApiVersions"] = "false",
            })
            .Build();
        services.AddSingleton<IConfiguration>(configuration);

        // Act
        services.AddGranitApiVersioning();

        // Assert — options are bound from configuration
        using ServiceProvider sp = services.BuildServiceProvider();
        GranitApiVersioningOptions options = sp.GetRequiredService<IOptions<GranitApiVersioningOptions>>().Value;
        options.DefaultMajorVersion.ShouldBe(2);
        options.ReportApiVersions.ShouldBeFalse();
    }

    [Fact]
    public void AddGranitApiVersioning_WithDefaultConfig_UsesDefaultOptions()
    {
        // Arrange
        ServiceCollection services = new();
        IConfiguration configuration = new ConfigurationBuilder().Build();
        services.AddSingleton<IConfiguration>(configuration);

        // Act
        services.AddGranitApiVersioning();

        // Assert
        using ServiceProvider sp = services.BuildServiceProvider();
        GranitApiVersioningOptions options = sp.GetRequiredService<IOptions<GranitApiVersioningOptions>>().Value;
        options.DefaultMajorVersion.ShouldBe(1);
        options.ReportApiVersions.ShouldBeTrue();
    }

    [Fact]
    public void AddGranitApiVersioning_ReturnsServices_ForChaining()
    {
        // Arrange
        ServiceCollection services = new();
        IConfiguration configuration = new ConfigurationBuilder().Build();
        services.AddSingleton<IConfiguration>(configuration);

        // Act
        IServiceCollection returned = services.AddGranitApiVersioning();

        // Assert
        returned.ShouldBeSameAs(services);
    }

    [Fact]
    public void AddGranitApiVersioning_ConfiguresApiVersioningOptions_WithDefaults()
    {
        // Arrange
        ServiceCollection services = new();
        IConfiguration configuration = new ConfigurationBuilder().Build();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddGranitApiVersioning();

        // Act — resolving IOptions triggers the AddApiVersioning lambda
        using ServiceProvider sp = services.BuildServiceProvider();
        ApiVersioningOptions options = sp.GetRequiredService<IOptions<ApiVersioningOptions>>().Value;

        // Assert
        options.DefaultApiVersion.ShouldBe(new ApiVersion(1));
        options.AssumeDefaultVersionWhenUnspecified.ShouldBeTrue();
        options.ReportApiVersions.ShouldBeTrue();
    }

    [Fact]
    public void AddGranitApiVersioning_ConfiguresApiVersioningOptions_WithCustomConfig()
    {
        // Arrange
        ServiceCollection services = new();
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Http:ApiVersioning:DefaultMajorVersion"] = "3",
                ["Http:ApiVersioning:ReportApiVersions"] = "false",
            })
            .Build();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddGranitApiVersioning();

        // Act
        using ServiceProvider sp = services.BuildServiceProvider();
        ApiVersioningOptions options = sp.GetRequiredService<IOptions<ApiVersioningOptions>>().Value;

        // Assert
        options.DefaultApiVersion.ShouldBe(new ApiVersion(3));
        options.ReportApiVersions.ShouldBeFalse();
    }

    [Fact]
    public void AddGranitApiVersioning_ConfiguresApiExplorerOptions()
    {
        // Arrange
        ServiceCollection services = new();
        IConfiguration configuration = new ConfigurationBuilder().Build();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddGranitApiVersioning();

        // Act — resolving IOptions triggers the AddApiExplorer lambda
        using ServiceProvider sp = services.BuildServiceProvider();
        ApiExplorerOptions options = sp.GetRequiredService<IOptions<ApiExplorerOptions>>().Value;

        // Assert
        options.GroupNameFormat.ShouldBe("'v'VVV");
        options.SubstituteApiVersionInUrl.ShouldBeTrue();
    }
}
