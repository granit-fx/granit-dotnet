using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Granit.DataExchange.Endpoints.Dtos.Import;
using Granit.DataExchange.Endpoints.Extensions;
using Granit.DataExchange.Endpoints.Permissions;
using Granit.DataExchange.Export;
using Granit.DataExchange.Import.Domain;
using Granit.DataExchange.Import.Mapping;
using Granit.DataExchange.Import.Parsing;
using Granit.DataExchange.Import.Pipeline;
using Granit.DataExchange.Import.Reporting;
using Granit.Domain.ValueObjects;
using Granit.Guids;
using Granit.Timing;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.Endpoints.Tests.Import;

/// <summary>
/// Integration tests for report and correction file endpoints.
/// </summary>
public sealed class ImportReportEndpointsTests : IAsyncDisposable
{
    private const string AdminRole = "granit-data-exchange-admin";
    private const string Prefix = "/data-exchange/import";

    private readonly IImportJobReader _jobReader = Substitute.For<IImportJobReader>();
    private readonly IDataExchangeFileProvider _fileProvider = Substitute.For<IDataExchangeFileProvider>();
    private readonly ICorrectionFileGenerator _correctionGenerator = Substitute.For<ICorrectionFileGenerator>();
    private readonly WebApplication _app;
    private readonly HttpClient _adminClient;

    public ImportReportEndpointsTests()
    {
        _fileProvider.OpenAsync(Arg.Any<BlobReference>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes("Name,Email\nAlice,alice@test.com"))));

        _correctionGenerator.GenerateAsync(
                Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<ImportReport>(),
                Arg.Any<FileParsingOptions>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes("Name,Email,Error\nAlice,,Missing Email"))));

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services
            .AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthHandler.SchemeName, _ => { });

        builder.Services.AddAuthorizationBuilder()
            .AddPolicy(DataExchangePermissions.Imports.Execute, policy => policy.RequireRole(AdminRole))
            .AddPolicy(DataExchangePermissions.Exports.Execute, policy => policy.RequireRole(AdminRole));
        builder.Services.AddSingleton(_jobReader);
        builder.Services.AddSingleton(Substitute.For<IImportJobWriter>());
        builder.Services.AddSingleton(_fileProvider);
        builder.Services.AddSingleton<ICorrectionFileGenerator>(_correctionGenerator);

        // Required by upload endpoints (all endpoints are compiled at startup)
        builder.Services.AddSingleton(Substitute.For<IClock>());
        builder.Services.AddSingleton(Substitute.For<IMappingSuggestionService>());
        builder.Services.AddSingleton(Substitute.For<IImportDefinitionDescriptor>());
        builder.Services.AddSingleton(Substitute.For<IFileParser>());
        builder.Services.AddSingleton(Substitute.For<Granit.Commands.ICommandSender>());
        builder.Services.AddSingleton(Substitute.For<IImportOrchestrator>());
        builder.Services.AddSingleton<IGuidGenerator>(new SimpleGuidGenerator());

        // Required by export endpoints (all endpoints are compiled at startup)
        builder.Services.AddSingleton(Substitute.For<IExportOrchestrator>());
        builder.Services.AddSingleton(Substitute.For<IExportPresetReader>());
        builder.Services.AddSingleton(Substitute.For<IExportPresetWriter>());
        builder.Services.AddSingleton(Substitute.For<IExportJobReader>());

        _app = builder.Build();
        _app.MapGranitDataExchange();
        _app.StartAsync().GetAwaiter().GetResult();

        _adminClient = BuildClient(AdminRole);
    }

    public async ValueTask DisposeAsync() => await _app.DisposeAsync();

    // ── GET /{jobId}/report ─────────────────────────────────────────────────

    [Fact]
    public async Task GetReport_WhenJobHasReport_Returns200()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        ImportReport report = BuildReport();
        ImportJob job = BuildJob(jobId, ImportJobStatus.Completed, JsonSerializer.Serialize(report));
        _jobReader.GetAsync(jobId, Arg.Any<CancellationToken>()).Returns(job);

        // Act
        HttpResponseMessage response = await _adminClient.GetAsync(
            $"{Prefix}/{jobId}/report", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        ImportReportResponse? result = await response.Content.ReadFromJsonAsync<ImportReportResponse>(TestContext.Current.CancellationToken);
        result.ShouldNotBeNull();
        result!.TotalRows.ShouldBe(10);
        result.FailedRows.ShouldBe(2);
    }

    [Fact]
    public async Task GetReport_WhenJobNotFound_Returns404()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        _jobReader.GetAsync(jobId, Arg.Any<CancellationToken>()).Returns((ImportJob?)null);

        // Act
        HttpResponseMessage response = await _adminClient.GetAsync(
            $"{Prefix}/{jobId}/report", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetReport_WhenNoReportJson_Returns404()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        ImportJob job = BuildJob(jobId, ImportJobStatus.Executing, reportJson: null);
        _jobReader.GetAsync(jobId, Arg.Any<CancellationToken>()).Returns(job);

        // Act
        HttpResponseMessage response = await _adminClient.GetAsync(
            $"{Prefix}/{jobId}/report", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // ── GET /{jobId}/correction-file ────────────────────────────────────────

    [Fact]
    public async Task GetCorrectionFile_WhenErrorsExist_ReturnsFile()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        ImportReport report = BuildReport();
        ImportJob job = BuildJob(jobId, ImportJobStatus.PartiallyCompleted, JsonSerializer.Serialize(report));
        _jobReader.GetAsync(jobId, Arg.Any<CancellationToken>()).Returns(job);

        // Act
        HttpResponseMessage response = await _adminClient.GetAsync(
            $"{Prefix}/{jobId}/correction-file", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("text/csv");
        string content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        content.ShouldContain("Error");
    }

    [Fact]
    public async Task GetCorrectionFile_WhenNoErrors_Returns204()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        ImportReport report = new()
        {
            TotalRows = 10,
            SucceededRows = 10,
            FailedRows = 0,
            SkippedRows = 0,
            InsertedRows = 10,
            UpdatedRows = 0,
            Duration = TimeSpan.FromSeconds(1),
            FinalStatus = ImportJobStatus.Completed,
            RowErrors = [],
        };
        ImportJob job = BuildJob(jobId, ImportJobStatus.Completed, JsonSerializer.Serialize(report));
        _jobReader.GetAsync(jobId, Arg.Any<CancellationToken>()).Returns(job);

        // Act
        HttpResponseMessage response = await _adminClient.GetAsync(
            $"{Prefix}/{jobId}/correction-file", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task GetCorrectionFile_WhenJobNotFound_Returns404()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        _jobReader.GetAsync(jobId, Arg.Any<CancellationToken>()).Returns((ImportJob?)null);

        // Act
        HttpResponseMessage response = await _adminClient.GetAsync(
            $"{Prefix}/{jobId}/correction-file", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private HttpClient BuildClient(string role)
    {
        HttpClient client = _app.GetTestClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, role);
        return client;
    }

    private static ImportJob BuildJob(Guid id, ImportJobStatus status, string? reportJson)
    {
        var job = ImportJob.Create(id, "Test.Import", "Object", "test.csv", "text/csv", 100, "blob-ref-1");
        job.CreatedAt = DateTimeOffset.UtcNow;

        if (status == ImportJobStatus.Completed && reportJson is not null)
        {
            job.MarkAsPreviewed();
            job.ConfirmMappings("[]");
            job.MarkAsExecuting();
            job.Complete(ImportJobStatus.Completed, reportJson, DateTimeOffset.UtcNow);
        }
        else if (status == ImportJobStatus.PartiallyCompleted && reportJson is not null)
        {
            job.MarkAsPreviewed();
            job.ConfirmMappings("[]");
            job.MarkAsExecuting();
            job.Complete(ImportJobStatus.PartiallyCompleted, reportJson, DateTimeOffset.UtcNow);
        }
        else if (status == ImportJobStatus.Failed && reportJson is not null)
        {
            job.MarkAsPreviewed();
            job.ConfirmMappings("[]");
            job.MarkAsExecuting();
            job.Complete(ImportJobStatus.Failed, reportJson, DateTimeOffset.UtcNow);
        }
        else if (status == ImportJobStatus.Executing)
        {
            job.MarkAsPreviewed();
            job.ConfirmMappings("[]");
            job.MarkAsExecuting();
        }

        return job;
    }

    private static ImportReport BuildReport() =>
        new()
        {
            TotalRows = 10,
            SucceededRows = 8,
            FailedRows = 2,
            SkippedRows = 0,
            InsertedRows = 6,
            UpdatedRows = 2,
            Duration = TimeSpan.FromSeconds(5),
            FinalStatus = ImportJobStatus.PartiallyCompleted,
            RowErrors =
            [
                new ImportRowError(3, ImportRowErrorKind.Validation, ["Required"], "Name is required"),
                new ImportRowError(7, ImportRowErrorKind.Persistence, ["Duplicate"], "Duplicate entry"),
            ],
        };
}
