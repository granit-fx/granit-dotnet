using System.Net;
using System.Net.Http.Json;
using Granit.DataExchange;
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
/// Integration tests for export preset CRUD endpoints.
/// </summary>
public sealed class ExportPresetEndpointsTests : IAsyncDisposable
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

    public ExportPresetEndpointsTests()
    {
        _descriptor = Substitute.For<IExportDefinitionDescriptor>();
        _descriptor.Name.Returns("Test.Export");
        _descriptor.EntityType.Returns(typeof(object));
        _descriptor.QueryDefinitionName.Returns((string?)null);
        _descriptor.SupportedFormats.Returns(new[] { "xlsx", "csv" });
        _descriptor.GetFields().Returns([
            new ExportFieldDescriptor("Name", "String", "Nom", null, 0, false),
            new ExportFieldDescriptor("Email", "String", null, null, 1, false),
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
        builder.Services.AddSingleton(Substitute.For<IDataExchangeFileProvider>());
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

    // ── GET /export/presets/{definitionName} ─────────────────────────────

    [Fact]
    public async Task ListPresets_returns_presets_for_definition()
    {
        // Arrange
        _presetReader.ListAsync("Test.Export", Arg.Any<CancellationToken>())
            .Returns([
                new ExportPreset("Test.Export", "Monthly", ["Name", "Email"], "xlsx", false),
                new ExportPreset("Test.Export", "Quick", ["Name"], "csv", true),
            ]);

        // Act
        HttpResponseMessage response = await _adminClient.GetAsync(
            $"{MetadataPrefix}/presets/Test.Export", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        ExportPresetResponse[]? presets = await response.Content
            .ReadFromJsonAsync<ExportPresetResponse[]>(TestContext.Current.CancellationToken);
        presets.ShouldNotBeNull();
        presets!.Length.ShouldBe(2);
        presets[0].PresetName.ShouldBe("Monthly");
        presets[1].PresetName.ShouldBe("Quick");
    }

    [Fact]
    public async Task ListPresets_empty_returns_empty_list()
    {
        // Arrange
        _presetReader.ListAsync("Test.Export", Arg.Any<CancellationToken>())
            .Returns(Array.Empty<ExportPreset>());

        // Act
        HttpResponseMessage response = await _adminClient.GetAsync(
            $"{MetadataPrefix}/presets/Test.Export", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        ExportPresetResponse[]? presets = await response.Content
            .ReadFromJsonAsync<ExportPresetResponse[]>(TestContext.Current.CancellationToken);
        presets.ShouldNotBeNull();
        presets!.Length.ShouldBe(0);
    }

    [Fact]
    public async Task ListPresets_without_auth_returns_401()
    {
        // Act
        HttpResponseMessage response = await _anonClient.GetAsync(
            $"{MetadataPrefix}/presets/Test.Export", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ── POST /export/presets ────────────────────────────────────────────

    [Fact]
    public async Task SavePreset_valid_request_returns_201()
    {
        // Arrange
        SaveExportPresetRequest request = new("Test.Export", "Monthly", ["Name", "Email"], "xlsx", false);

        // Act
        HttpResponseMessage response = await _adminClient.PostAsJsonAsync(
            $"{MetadataPrefix}/presets", request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        await _presetWriter.Received(1).SaveAsync(
            Arg.Is<ExportPreset>(p => p.PresetName == "Monthly" && p.DefinitionName == "Test.Export"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SavePreset_unknown_definition_returns_400()
    {
        // Arrange
        SaveExportPresetRequest request = new("Unknown.Export", "Monthly", ["Name"], "xlsx", false);

        // Act
        HttpResponseMessage response = await _adminClient.PostAsJsonAsync(
            $"{MetadataPrefix}/presets", request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SavePreset_empty_preset_name_returns_400()
    {
        // Arrange
        SaveExportPresetRequest request = new("Test.Export", "", ["Name"], "xlsx", false);

        // Act
        HttpResponseMessage response = await _adminClient.PostAsJsonAsync(
            $"{MetadataPrefix}/presets", request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SavePreset_empty_fields_returns_400()
    {
        // Arrange
        SaveExportPresetRequest request = new("Test.Export", "Monthly", [], "xlsx", false);

        // Act
        HttpResponseMessage response = await _adminClient.PostAsJsonAsync(
            $"{MetadataPrefix}/presets", request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // ── DELETE /export/presets/{definitionName}/{presetName} ─────────────

    [Fact]
    public async Task DeletePreset_existing_returns_204()
    {
        // Arrange
        _presetReader.GetAsync("Test.Export", "Monthly", Arg.Any<CancellationToken>())
            .Returns(new ExportPreset("Test.Export", "Monthly", ["Name"], "xlsx", false));

        // Act
        HttpResponseMessage response = await _adminClient.DeleteAsync(
            $"{MetadataPrefix}/presets/Test.Export/Monthly", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await _presetWriter.Received(1).DeleteAsync("Test.Export", "Monthly", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeletePreset_nonexistent_returns_404()
    {
        // Arrange
        _presetReader.GetAsync("Test.Export", "NonExistent", Arg.Any<CancellationToken>())
            .Returns((ExportPreset?)null);

        // Act
        HttpResponseMessage response = await _adminClient.DeleteAsync(
            $"{MetadataPrefix}/presets/Test.Export/NonExistent", TestContext.Current.CancellationToken);

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
