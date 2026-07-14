// =============================================================================
// Tests - API versioning registration (merged from Granit.Http.ApiVersioning)
// =============================================================================
// Vérifie que AddGranitApiDocumentation enregistre Asp.Versioning et que
// ApiDocumentationOptions (DefaultMajorVersion / ReportApiVersions) est la
// source de vérité unique pour ApiVersioningOptions et ApiExplorerOptions.
// =============================================================================

using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using Granit.Http.ApiDocumentation.Extensions;
using Granit.Http.ApiDocumentation.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Http.ApiDocumentation.Tests;

public sealed class ApiVersioningRegistrationTests
{
    [Fact]
    public void AddGranitApiDocumentation_RegistersApiVersioningServices()
    {
        // Arrange
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();

        // Act
        builder.AddGranitApiDocumentation();

        // Assert — IApiVersionReader, IApiVersionSelector, etc. should be registered
        ServiceDescriptor? descriptor = builder.Services.FirstOrDefault(
            d => d.ServiceType.Name.Contains("ApiVersion", StringComparison.OrdinalIgnoreCase));
        descriptor.ShouldNotBeNull("AddApiVersioning must register versioning services");
    }

    [Fact]
    public void AddGranitApiDocumentation_ConfiguresApiVersioningOptions_WithDefaults()
    {
        // Arrange
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.AddGranitApiDocumentation();

        // Act — resolving IOptions triggers the deferred configuration lambda
        using ServiceProvider sp = builder.Services.BuildServiceProvider();
        ApiVersioningOptions options = sp.GetRequiredService<IOptions<ApiVersioningOptions>>().Value;

        // Assert
        options.DefaultApiVersion.ShouldBe(new ApiVersion(1));
        options.AssumeDefaultVersionWhenUnspecified.ShouldBeTrue();
        options.ReportApiVersions.ShouldBeTrue();
    }

    [Fact]
    public void AddGranitApiDocumentation_ConfiguresApiVersioningOptions_FromApiDocumentationSection()
    {
        // Arrange — single source of truth: Http:ApiDocumentation drives Asp.Versioning
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Http:ApiDocumentation:MajorVersions:0"] = "1",
            ["Http:ApiDocumentation:MajorVersions:1"] = "3",
            ["Http:ApiDocumentation:DefaultMajorVersion"] = "3",
            ["Http:ApiDocumentation:ReportApiVersions"] = "false",
        });
        builder.AddGranitApiDocumentation();

        // Act
        using ServiceProvider sp = builder.Services.BuildServiceProvider();
        ApiVersioningOptions options = sp.GetRequiredService<IOptions<ApiVersioningOptions>>().Value;

        // Assert
        options.DefaultApiVersion.ShouldBe(new ApiVersion(3));
        options.ReportApiVersions.ShouldBeFalse();
    }

    [Fact]
    public void AddGranitApiDocumentation_ConfiguresApiExplorerOptions()
    {
        // Arrange
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.AddGranitApiDocumentation();

        // Act — resolving IOptions triggers the AddApiExplorer lambda
        using ServiceProvider sp = builder.Services.BuildServiceProvider();
        ApiExplorerOptions options = sp.GetRequiredService<IOptions<ApiExplorerOptions>>().Value;

        // Assert
        options.GroupNameFormat.ShouldBe("'v'VVV");
        options.SubstituteApiVersionInUrl.ShouldBeTrue();
    }

    [Fact]
    public void DefaultMajorVersion_OutsideMajorVersions_FailsValidation()
    {
        // Arrange — DefaultMajorVersion=2 but only v1 is documented/routable
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Http:ApiDocumentation:MajorVersions:0"] = "1",
            ["Http:ApiDocumentation:DefaultMajorVersion"] = "2",
        });
        builder.AddGranitApiDocumentation();

        // Act & Assert — options validation runs on first access
        using ServiceProvider sp = builder.Services.BuildServiceProvider();
        Should.Throw<OptionsValidationException>(() =>
            sp.GetRequiredService<IOptions<ApiDocumentationOptions>>().Value);
    }

    [Fact]
    public void DefaultMajorVersion_WithEmptyMajorVersions_UsesImplicitVersionOne()
    {
        // Arrange — no MajorVersions configured → implicit [1]; default 1 is valid
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.AddGranitApiDocumentation();

        // Act
        using ServiceProvider sp = builder.Services.BuildServiceProvider();
        ApiDocumentationOptions options =
            sp.GetRequiredService<IOptions<ApiDocumentationOptions>>().Value;

        // Assert
        options.DefaultMajorVersion.ShouldBe(1);
    }
}
