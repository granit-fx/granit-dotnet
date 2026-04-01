using System.Net;
using System.Net.Http.Json;
using Granit.DataExchange.Endpoints.Dtos.Export;
using Granit.DataExchange.Endpoints.Extensions;
using Granit.DataExchange.Endpoints.Permissions;
using Granit.DataExchange.Export;
using Granit.DataExchange.Import.Domain;
using Granit.DataExchange.Import.Mapping;
using Granit.DataExchange.Import.Parsing;
using Granit.DataExchange.Import.Pipeline;
using Granit.Guids;
using Granit.Timing;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.Endpoints.Tests.Export;

/// <summary>
/// Integration tests for export definition listing and field introspection endpoints.
/// </summary>
public sealed class ExportDefinitionEndpointsTests : IAsyncDisposable
{
    private const string AdminRole = "granit-data-exchange-admin";
    private const string MetadataPrefix = "/data-exchange/metadata";

    private readonly IExportOrchestrator _orchestrator = Substitute.For<IExportOrchestrator>();
    private readonly IExportPresetReader _presetReader = Substitute.For<IExportPresetReader>();
    private readonly IExportPresetWriter _presetWriter = Substitute.For<IExportPresetWriter>();
    private readonly IExportDefinitionDescriptor _descriptor;
    private readonly WebApplication _app;
    private readonly HttpClient _adminClient;
    private readonly HttpClient _anonClient;

    public ExportDefinitionEndpointsTests()
    {
        _descriptor = Substitute.For<IExportDefinitionDescriptor>();
        _descriptor.Name.Returns("Test.Export");
        _descriptor.EntityType.Returns(typeof(object));
        _descriptor.QueryDefinitionName.Returns((string?)null);
        _descriptor.SupportedFormats.Returns(new[] { "xlsx", "csv" });
        _descriptor.GetFields().Returns([
            new ExportFieldDescriptor("Name", "String", "Nom", null, 0, false),
            new ExportFieldDescriptor("Email", "String", null, null, 1, false),
            new ExportFieldDescriptor("Company.Name", "String", "Société", null, 2, true),
        ]);

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services
            .AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthHandler.SchemeName, _ => { });

        builder.Services.AddAuthorizationBuilder()
            .AddPolicy(DataExchangePermissions.Imports.Execute, policy => policy.RequireRole(AdminRole))
            .AddPolicy(DataExchangePermissions.Exports.Execute, policy => policy.RequireRole(AdminRole));
        builder.Services.AddSingleton(_orchestrator);
        builder.Services.AddSingleton(_presetReader);
        builder.Services.AddSingleton(_presetWriter);
        builder.Services.AddSingleton(_descriptor);
        builder.Services.AddSingleton(Substitute.For<IExportJobReader>());

        // Required by import endpoints (compiled at startup)
        builder.Services.AddSingleton(Substitute.For<IImportJobReader>());
        builder.Services.AddSingleton(Substitute.For<IImportJobWriter>());
        builder.Services.AddSingleton(Substitute.For<IImportFileProvider>());
        builder.Services.AddSingleton(Substitute.For<IMappingSuggestionService>());
        builder.Services.AddSingleton(Substitute.For<IClock>());
        builder.Services.AddSingleton(Substitute.For<IImportDefinitionDescriptor>());
        builder.Services.AddSingleton(Substitute.For<IFileParser>());
        builder.Services.AddSingleton<IGuidGenerator>(new SimpleGuidGenerator());

        _app = builder.Build();
        _app.MapGranitDataExchange();
        _app.StartAsync().GetAwaiter().GetResult();

        _adminClient = BuildClient(AdminRole);
        _anonClient = _app.GetTestClient();
    }

    public async ValueTask DisposeAsync() => await _app.DisposeAsync();

    // ── GET /export/definitions ─────────────────────────────────────────

    [Fact]
    public async Task ListDefinitions_returns_registered_definitions()
    {
        // Act
        HttpResponseMessage response = await _adminClient.GetAsync(
            $"{MetadataPrefix}/definitions", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        ExportDefinitionResponse[]? definitions = await response.Content
            .ReadFromJsonAsync<ExportDefinitionResponse[]>(TestContext.Current.CancellationToken);
        definitions.ShouldNotBeNull();
        definitions!.Length.ShouldBe(1);
        definitions[0].Name.ShouldBe("Test.Export");
    }

    [Fact]
    public async Task ListDefinitions_without_auth_returns_401()
    {
        // Act
        HttpResponseMessage response = await _anonClient.GetAsync(
            $"{MetadataPrefix}/definitions", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ── GET /export/definitions/{name}/fields ───────────────────────────

    [Fact]
    public async Task GetFields_existing_definition_returns_fields()
    {
        // Act
        HttpResponseMessage response = await _adminClient.GetAsync(
            $"{MetadataPrefix}/definitions/Test.Export/fields", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        ExportFieldResponse[]? fields = await response.Content
            .ReadFromJsonAsync<ExportFieldResponse[]>(TestContext.Current.CancellationToken);
        fields.ShouldNotBeNull();
        fields!.Length.ShouldBe(3);
        fields[0].PropertyPath.ShouldBe("Name");
        fields[0].Header.ShouldBe("Nom");
        fields[2].PropertyPath.ShouldBe("Company.Name");
        fields[2].IsNavigation.ShouldBeTrue();
    }

    [Fact]
    public async Task GetFields_unknown_definition_returns_404()
    {
        // Act
        HttpResponseMessage response = await _adminClient.GetAsync(
            $"{MetadataPrefix}/definitions/Unknown.Export/fields", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private HttpClient BuildClient(string role)
    {
        HttpClient client = _app.GetTestClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, role);
        return client;
    }
}
