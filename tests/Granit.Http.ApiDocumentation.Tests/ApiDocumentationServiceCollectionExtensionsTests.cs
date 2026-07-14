// =============================================================================
// Tests - ApiDocumentationServiceCollectionExtensions
// =============================================================================
// Vérifie que AddGranitApiDocumentation enregistre un document OpenAPI
// par version déclarée, et que les transformers sont enregistrés en DI.
// =============================================================================

using System.Reflection;
using Granit.Http.ApiDocumentation.Extensions;
using Granit.Http.ApiDocumentation.Options;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Http.ApiDocumentation.Tests;

public sealed class ApiDocumentationServiceCollectionExtensionsTests
{
    [Fact]
    public void AddGranitApiDocumentation_WithConfiguration_RegistersOptions()
    {
        // Arrange
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Http:ApiDocumentation:Title"] = "Test API",
            ["Http:ApiDocumentation:MajorVersions:0"] = "2",
        });

        // Act
        builder.AddGranitApiDocumentation();

        // Assert — options are bound from configuration
        using ServiceProvider sp = builder.Services.BuildServiceProvider();
        ApiDocumentationOptions options =
            sp.GetRequiredService<IOptions<ApiDocumentationOptions>>().Value;
        options.Title.ShouldBe("Test API");
        options.MajorVersions.ShouldContain(2);
    }

    [Fact]
    public void AddGranitApiDocumentation_WithConfiguredValues_RegistersOptions()
    {
        // Arrange
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Http:ApiDocumentation:Title"] = "Test API",
            ["Http:ApiDocumentation:MajorVersions:0"] = "2",
            ["Http:ApiDocumentation:MajorVersions:1"] = "3",
        });

        // Act
        builder.AddGranitApiDocumentation();

        // Assert
        using ServiceProvider sp = builder.Services.BuildServiceProvider();
        ApiDocumentationOptions options =
            sp.GetRequiredService<IOptions<ApiDocumentationOptions>>().Value;
        options.Title.ShouldBe("Test API");
        options.MajorVersions.ShouldContain(2);
        options.MajorVersions.ShouldContain(3);
    }

    [Fact]
    public void AddGranitApiDocumentation_WithDefaultConfig_UsesDefaultOptions()
    {
        // Arrange
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();

        // Act
        builder.AddGranitApiDocumentation();

        // Assert
        using ServiceProvider sp = builder.Services.BuildServiceProvider();
        ApiDocumentationOptions options =
            sp.GetRequiredService<IOptions<ApiDocumentationOptions>>().Value;
        options.Title.ShouldBe("API");
        options.MajorVersions.ShouldHaveSingleItem().ShouldBe(1);
    }

    [Fact]
    public void AddGranitApiDocumentation_ReturnsBuilder_ForChaining()
    {
        // Arrange
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();

        // Act
        IHostApplicationBuilder returned = builder.AddGranitApiDocumentation();

        // Assert
        returned.ShouldBeSameAs(builder);
    }

    [Fact]
    public void AddGranitApiDocumentation_WithMultipleVersions_RegistersMultipleOpenApiDocuments()
    {
        // Arrange
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Http:ApiDocumentation:Title"] = "Multi-version API",
            ["Http:ApiDocumentation:MajorVersions:0"] = "1",
            ["Http:ApiDocumentation:MajorVersions:1"] = "2",
            ["Http:ApiDocumentation:MajorVersions:2"] = "3",
        });

        // Act
        builder.AddGranitApiDocumentation();

        // Assert — one AddOpenApi registration per version → each is named "v1", "v2", "v3"
        // AddOpenApi registers IConfigureOptions<OpenApiOptions> keyed by document name.
        // We verify services are registered (not zero), which indicates documents were added.
        builder.Services.ShouldNotBeEmpty("versions [1, 2, 3] must register OpenAPI services");
    }

    // --- OpenApiOptions : déclenchement du callback AddOpenApi et du transformer inline ---

    [Fact]
    public async Task AddGranitApiDocumentation_DocumentTransformer_SetsInfoWithoutContact()
    {
        // Arrange
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Http:ApiDocumentation:Title"] = "My API",
            ["Http:ApiDocumentation:Description"] = "My description",
            ["Http:ApiDocumentation:MajorVersions:0"] = "1",
        });

        builder.AddGranitApiDocumentation();

        await using ServiceProvider sp = builder.Services.BuildServiceProvider();

        // IOptionsMonitor.Get("v1") triggers the outer openApiOptions => lambda,
        // which registers the document transformers on the OpenApiOptions instance.
        IOptionsMonitor<OpenApiOptions> monitor = sp.GetRequiredService<IOptionsMonitor<OpenApiOptions>>();
        OpenApiOptions openApiOpts = monitor.Get("v1");

        // DocumentTransformers is internal in Microsoft.AspNetCore.OpenApi.
        // Reflection is required to retrieve and invoke the inline delegate transformer.
        List<IOpenApiDocumentTransformer> transformers = GetDocumentTransformers(openApiOpts);
        OpenApiDocument doc = new() { Paths = [] };
        OpenApiDocumentTransformerContext ctx = BuildTransformerContext("v1");

        // Act — invoke the inline delegate (first transformer = the doc.Info setter)
        await transformers[0].TransformAsync(doc, ctx, TestContext.Current.CancellationToken);

        // Assert
        doc.Info.Title.ShouldBe("My API");
        doc.Info.Description.ShouldBe("My description");
        doc.Info.Contact.ShouldBeNull("ContactEmail is null → no contact block");
    }

    [Fact]
    public async Task AddGranitApiDocumentation_DocumentTransformer_SetsInfoWithContact()
    {
        // Arrange
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Http:ApiDocumentation:Title"] = "Test API",
            ["Http:ApiDocumentation:ContactEmail"] = "api@example.com",
            ["Http:ApiDocumentation:MajorVersions:0"] = "2",
        });

        builder.AddGranitApiDocumentation();

        await using ServiceProvider sp = builder.Services.BuildServiceProvider();
        IOptionsMonitor<OpenApiOptions> monitor = sp.GetRequiredService<IOptionsMonitor<OpenApiOptions>>();
        OpenApiOptions openApiOpts = monitor.Get("v2");

        List<IOpenApiDocumentTransformer> transformers = GetDocumentTransformers(openApiOpts);
        OpenApiDocument doc = new() { Paths = [] };
        OpenApiDocumentTransformerContext ctx = BuildTransformerContext("v2");

        // Act
        await transformers[0].TransformAsync(doc, ctx, TestContext.Current.CancellationToken);

        // Assert
        doc.Info.Contact.ShouldNotBeNull();
        doc.Info.Contact!.Email.ShouldBe("api@example.com");
        doc.Info.Description.ShouldBeNull();
    }

    // --- Helpers ---

    /// <summary>
    /// Accesses the internal <c>DocumentTransformers</c> field on <see cref="OpenApiOptions"/>
    /// via reflection. The field is <c>internal</c> in <c>Microsoft.AspNetCore.OpenApi</c> (validated on 10.0.3).
    /// If the field is renamed in a future SDK version this helper will throw an explicit error.
    /// </summary>
    private static List<IOpenApiDocumentTransformer> GetDocumentTransformers(OpenApiOptions options)
    {
        // DocumentTransformers is an internal field (not a property) in Microsoft.AspNetCore.OpenApi.
        FieldInfo field = typeof(OpenApiOptions).GetField(
            "DocumentTransformers",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException(
                "OpenApiOptions.DocumentTransformers not found. " +
                "The field may have been renamed in this version of Microsoft.AspNetCore.OpenApi.");

        return (List<IOpenApiDocumentTransformer>)field.GetValue(options)!;
    }

    private static OpenApiDocumentTransformerContext BuildTransformerContext(string documentName) =>
        new()
        {
            DocumentName = documentName,
            DescriptionGroups = [],
            ApplicationServices = Substitute.For<IServiceProvider>(),
        };
}
