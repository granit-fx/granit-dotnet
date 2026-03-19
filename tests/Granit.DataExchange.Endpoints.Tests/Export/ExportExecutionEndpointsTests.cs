using System.Net;
using System.Net.Http.Json;
using Granit.DataExchange.Endpoints.Dtos.Export;
using Granit.DataExchange.Endpoints.Extensions;
using Granit.DataExchange.Export;
using Granit.DataExchange.Export.Domain;
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
/// Integration tests for export job creation, status polling, and file download endpoints.
/// </summary>
public sealed class ExportExecutionEndpointsTests : IAsyncDisposable
{
    private const string AdminRole = "granit-data-exchange-admin";
    private const string ExportPrefix = "/data-exchange/export";

    private readonly IExportOrchestrator _orchestrator = Substitute.For<IExportOrchestrator>();
    private readonly IExportPresetReader _presetReader = Substitute.For<IExportPresetReader>();
    private readonly IExportPresetWriter _presetWriter = Substitute.For<IExportPresetWriter>();
    private readonly IExportDefinitionDescriptor _descriptor;
    private readonly WebApplication _app;
    private readonly HttpClient _adminClient;
    private readonly HttpClient _userClient;
    private readonly HttpClient _anonClient;

    public ExportExecutionEndpointsTests()
    {
        _descriptor = Substitute.For<IExportDefinitionDescriptor>();
        _descriptor.Name.Returns("Test.Export");
        _descriptor.EntityType.Returns(typeof(object));
        _descriptor.QueryDefinitionName.Returns((string?)null);
        _descriptor.SupportedFormats.Returns(new[] { "xlsx", "csv" });
        _descriptor.GetFields().Returns([
            new ExportFieldDescriptor("Name", "String", "Nom", null, 0, false),
        ]);

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services
            .AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthHandler.SchemeName, _ => { });

        builder.Services.AddAuthorization();
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
        _app.MapDataExchangeEndpoints();
        _app.StartAsync().GetAwaiter().GetResult();

        _adminClient = BuildClient(AdminRole);
        _userClient = BuildClient("regular-user");
        _anonClient = _app.GetTestClient();
    }

    public async ValueTask DisposeAsync() => await _app.DisposeAsync();

    // ── POST /export/jobs ───────────────────────────────────────────────

    [Fact]
    public async Task CreateExportJob_valid_request_returns_201()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        _orchestrator.ExportAsync(Arg.Any<ExportRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ExportJobResult(jobId, ExportJobStatus.Queued));
        _orchestrator.GetJobAsync(jobId, Arg.Any<CancellationToken>())
            .Returns(ExportJob.Create(jobId, "Test.Export", "csv", "{}"));

        CreateExportJobRequest request = new("Test.Export", "csv", null, false, null, null, null, null);

        // Act
        HttpResponseMessage response = await _adminClient.PostAsJsonAsync(
            $"{ExportPrefix}/jobs", request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        ExportJobResponse? result = await response.Content
            .ReadFromJsonAsync<ExportJobResponse>(TestContext.Current.CancellationToken);
        result.ShouldNotBeNull();
        result!.Id.ShouldBe(jobId);
        result.Status.ShouldBe(ExportJobStatus.Queued);
    }

    [Fact]
    public async Task CreateExportJob_unknown_definition_returns_400()
    {
        // Arrange
        CreateExportJobRequest request = new("Unknown.Export", "csv", null, false, null, null, null, null);

        // Act
        HttpResponseMessage response = await _adminClient.PostAsJsonAsync(
            $"{ExportPrefix}/jobs", request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateExportJob_unsupported_format_returns_400()
    {
        // Arrange
        CreateExportJobRequest request = new("Test.Export", "pdf", null, false, null, null, null, null);

        // Act
        HttpResponseMessage response = await _adminClient.PostAsJsonAsync(
            $"{ExportPrefix}/jobs", request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateExportJob_without_auth_returns_401()
    {
        // Arrange
        CreateExportJobRequest request = new("Test.Export", "csv", null, false, null, null, null, null);

        // Act
        HttpResponseMessage response = await _anonClient.PostAsJsonAsync(
            $"{ExportPrefix}/jobs", request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateExportJob_wrong_role_returns_403()
    {
        // Arrange
        CreateExportJobRequest request = new("Test.Export", "csv", null, false, null, null, null, null);

        // Act
        HttpResponseMessage response = await _userClient.PostAsJsonAsync(
            $"{ExportPrefix}/jobs", request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // ── GET /export/jobs/{jobId} ────────────────────────────────────────

    [Fact]
    public async Task GetJobStatus_existing_job_returns_200()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var completedJob = ExportJob.Create(jobId, "Test.Export", "xlsx", "{}");
        completedJob.Complete("blob-ref", "export.xlsx", 100, DateTimeOffset.UtcNow);
        _orchestrator.GetJobAsync(jobId, Arg.Any<CancellationToken>())
            .Returns(completedJob);

        // Act
        HttpResponseMessage response = await _adminClient.GetAsync(
            $"{ExportPrefix}/jobs/{jobId}", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        ExportJobResponse? result = await response.Content
            .ReadFromJsonAsync<ExportJobResponse>(TestContext.Current.CancellationToken);
        result.ShouldNotBeNull();
        result!.Status.ShouldBe(ExportJobStatus.Completed);
        result.RowCount.ShouldBe(100);
    }

    [Fact]
    public async Task GetJobStatus_unknown_job_returns_404()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        _orchestrator.GetJobAsync(jobId, Arg.Any<CancellationToken>())
            .Returns((ExportJob?)null);

        // Act
        HttpResponseMessage response = await _adminClient.GetAsync(
            $"{ExportPrefix}/jobs/{jobId}", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // ── GET /export/jobs/{jobId}/download ────────────────────────────────

    [Fact]
    public async Task Download_completed_job_returns_file()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var downloadJob = ExportJob.Create(jobId, "Test.Export", "csv", "{}");
        downloadJob.Complete("blob-ref", "export.csv", 10, DateTimeOffset.UtcNow);
        _orchestrator.GetJobAsync(jobId, Arg.Any<CancellationToken>())
            .Returns(downloadJob);

        byte[] fileContent = "Name;Email\nAlice;alice@test.com"u8.ToArray();
        _orchestrator.GetDownloadAsync(jobId, Arg.Any<CancellationToken>())
            .Returns(new ExportDownload(new MemoryStream(fileContent), "text/csv", "export.csv"));

        // Act
        HttpResponseMessage response = await _adminClient.GetAsync(
            $"{ExportPrefix}/jobs/{jobId}/download", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("text/csv");
    }

    [Fact]
    public async Task Download_non_completed_job_returns_400()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var exportingJob = ExportJob.Create(jobId, "Test.Export", "csv", "{}");
        exportingJob.MarkAsExporting();
        _orchestrator.GetJobAsync(jobId, Arg.Any<CancellationToken>())
            .Returns(exportingJob);

        // Act
        HttpResponseMessage response = await _adminClient.GetAsync(
            $"{ExportPrefix}/jobs/{jobId}/download", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Download_unknown_job_returns_404()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        _orchestrator.GetJobAsync(jobId, Arg.Any<CancellationToken>())
            .Returns((ExportJob?)null);

        // Act
        HttpResponseMessage response = await _adminClient.GetAsync(
            $"{ExportPrefix}/jobs/{jobId}/download", TestContext.Current.CancellationToken);

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
