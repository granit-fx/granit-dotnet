using System.Net;
using System.Net.Http.Json;
using Granit.Commands;
using Granit.DataExchange.Endpoints.Dtos.Import;
using Granit.DataExchange.Endpoints.Extensions;
using Granit.DataExchange.Endpoints.Permissions;
using Granit.DataExchange.Export;
using Granit.DataExchange.Import.Domain;
using Granit.DataExchange.Import.Mapping;
using Granit.DataExchange.Import.Messages;
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
/// Integration tests for execution, dry-run, status, and cancellation endpoints.
/// </summary>
public sealed class ImportExecutionEndpointsTests : IAsyncDisposable
{
    private const string AdminRole = "granit-data-exchange-admin";
    private const string Prefix = "/data-exchange/import";

    private readonly IImportJobReader _jobReader = Substitute.For<IImportJobReader>();
    private readonly IImportJobWriter _jobWriter = Substitute.For<IImportJobWriter>();
    private readonly ICommandSender _commandSender = Substitute.For<ICommandSender>();
    private readonly IImportOrchestrator _orchestrator = Substitute.For<IImportOrchestrator>();
    private readonly IDataExchangeFileProvider _fileProvider = Substitute.For<IDataExchangeFileProvider>();
    private readonly WebApplication _app;
    private readonly HttpClient _adminClient;
    private readonly HttpClient _anonClient;

    public ImportExecutionEndpointsTests()
    {
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
        builder.Services.AddSingleton(_jobWriter);
        builder.Services.AddSingleton(_commandSender);
        builder.Services.AddSingleton(_orchestrator);
        builder.Services.AddSingleton(_fileProvider);

        // Required by upload endpoints (all endpoints are compiled at startup)
        builder.Services.AddSingleton(Substitute.For<IClock>());
        builder.Services.AddSingleton(Substitute.For<IMappingSuggestionService>());
        builder.Services.AddSingleton(Substitute.For<IImportDefinitionDescriptor>());
        builder.Services.AddSingleton(Substitute.For<IFileParser>());
        builder.Services.AddSingleton<IGuidGenerator>(new SimpleGuidGenerator());

        // Required by export endpoints
        builder.Services.AddSingleton(Substitute.For<IExportOrchestrator>());
        builder.Services.AddSingleton(Substitute.For<IExportPresetReader>());
        builder.Services.AddSingleton(Substitute.For<IExportPresetWriter>());
        builder.Services.AddSingleton(Substitute.For<IExportJobReader>());

        _app = builder.Build();
        _app.MapGranitDataExchange();
        _app.StartAsync().GetAwaiter().GetResult();

        _adminClient = BuildClient(AdminRole);
        _anonClient = _app.GetTestClient();
    }

    public async ValueTask DisposeAsync() => await _app.DisposeAsync();

    // ── POST /{jobId}/execute ───────────────────────────────────────────────

    [Fact]
    public async Task Execute_WhenJobIsMapped_Returns202()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        ImportJob job = BuildJob(jobId, ImportJobStatus.Mapped);
        _jobReader.GetAsync(jobId, Arg.Any<CancellationToken>()).Returns(job);
        _commandSender.SendAsync(Arg.Any<ExecuteImportCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Act
        HttpResponseMessage response = await _adminClient.PostAsync(
            $"{Prefix}/{jobId}/execute", content: null, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        await _commandSender.Received(1).SendAsync(
            Arg.Is<ExecuteImportCommand>(c => c.ImportJobId == jobId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_WhenJobNotMapped_Returns400()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        ImportJob job = BuildJob(jobId, ImportJobStatus.Created);
        _jobReader.GetAsync(jobId, Arg.Any<CancellationToken>()).Returns(job);

        // Act
        HttpResponseMessage response = await _adminClient.PostAsync(
            $"{Prefix}/{jobId}/execute", content: null, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Execute_WhenJobNotFound_Returns404()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        _jobReader.GetAsync(jobId, Arg.Any<CancellationToken>()).Returns((ImportJob?)null);

        // Act
        HttpResponseMessage response = await _adminClient.PostAsync(
            $"{Prefix}/{jobId}/execute", content: null, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // ── POST /{jobId}/dry-run ───────────────────────────────────────────────

    [Fact]
    public async Task DryRun_WhenJobIsMapped_Returns200WithReport()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        ImportJob job = BuildJob(jobId, ImportJobStatus.Mapped);
        _jobReader.GetAsync(jobId, Arg.Any<CancellationToken>()).Returns(job);

        ImportReport report = BuildReport();
        _orchestrator.DryRunAsync(jobId, Arg.Any<CancellationToken>()).Returns(report);

        // Act
        HttpResponseMessage response = await _adminClient.PostAsync(
            $"{Prefix}/{jobId}/dry-run", content: null, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        ImportReportResponse? result = await response.Content.ReadFromJsonAsync<ImportReportResponse>(TestContext.Current.CancellationToken);
        result.ShouldNotBeNull();
        result!.ImportJobId.ShouldBe(jobId);
        result.TotalRows.ShouldBe(10);
    }

    [Fact]
    public async Task DryRun_WhenJobNotMapped_Returns400()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        ImportJob job = BuildJob(jobId, ImportJobStatus.Created);
        _jobReader.GetAsync(jobId, Arg.Any<CancellationToken>()).Returns(job);

        // Act
        HttpResponseMessage response = await _adminClient.PostAsync(
            $"{Prefix}/{jobId}/dry-run", content: null, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // ── GET /{jobId} (status) ───────────────────────────────────────────────

    [Fact]
    public async Task GetStatus_WhenJobExists_Returns200()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        ImportJob job = BuildJob(jobId, ImportJobStatus.Completed);
        _jobReader.GetAsync(jobId, Arg.Any<CancellationToken>()).Returns(job);

        // Act
        HttpResponseMessage response = await _adminClient.GetAsync(
            $"{Prefix}/{jobId}", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        ImportJobResponse? result = await response.Content.ReadFromJsonAsync<ImportJobResponse>(TestContext.Current.CancellationToken);
        result.ShouldNotBeNull();
        result!.Status.ShouldBe(ImportJobStatus.Completed);
    }

    [Fact]
    public async Task GetStatus_WhenJobNotFound_Returns404()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        _jobReader.GetAsync(jobId, Arg.Any<CancellationToken>()).Returns((ImportJob?)null);

        // Act
        HttpResponseMessage response = await _adminClient.GetAsync(
            $"{Prefix}/{jobId}", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // ── DELETE /{jobId} (cancel) ────────────────────────────────────────────

    [Fact]
    public async Task Cancel_WhenJobIsCancellable_Returns204()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        ImportJob job = BuildJob(jobId, ImportJobStatus.Mapped);
        _jobReader.GetAsync(jobId, Arg.Any<CancellationToken>()).Returns(job);
        _fileProvider.DeleteAsync(Arg.Any<BlobReference>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Act
        HttpResponseMessage response = await _adminClient.DeleteAsync(
            $"{Prefix}/{jobId}", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await _jobWriter.Received(1).UpdateAsync(Arg.Is<ImportJob>(j => j.Status == ImportJobStatus.Cancelled), Arg.Any<CancellationToken>());
        await _fileProvider.Received(1).DeleteAsync("blob-ref-1", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Cancel_WhenJobIsExecuting_Returns400()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        ImportJob job = BuildJob(jobId, ImportJobStatus.Executing);
        _jobReader.GetAsync(jobId, Arg.Any<CancellationToken>()).Returns(job);

        // Act
        HttpResponseMessage response = await _adminClient.DeleteAsync(
            $"{Prefix}/{jobId}", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Cancel_WhenJobNotFound_Returns404()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        _jobReader.GetAsync(jobId, Arg.Any<CancellationToken>()).Returns((ImportJob?)null);

        // Act
        HttpResponseMessage response = await _adminClient.DeleteAsync(
            $"{Prefix}/{jobId}", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // ── Auth ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Execute_WithoutAuth_Returns401()
    {
        // Arrange
        var jobId = Guid.NewGuid();

        // Act
        HttpResponseMessage response = await _anonClient.PostAsync(
            $"{Prefix}/{jobId}/execute", content: null, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private HttpClient BuildClient(string role)
    {
        HttpClient client = _app.GetTestClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, role);
        return client;
    }

    private static ImportJob BuildJob(Guid id, ImportJobStatus status)
    {
        var job = ImportJob.Create(id, "Test.Import", "Object", "test.csv", "text/csv", 100, "blob-ref-1");
        job.CreatedAt = DateTimeOffset.UtcNow;
        TransitionToStatus(job, status);
        return job;
    }

    private static void TransitionToStatus(ImportJob job, ImportJobStatus status)
    {
        if (status == ImportJobStatus.Previewed)
        {
            job.MarkAsPreviewed();
        }
        else if (status == ImportJobStatus.Mapped)
        {
            job.MarkAsPreviewed();
            job.ConfirmMappings([]);
        }
        else if (status == ImportJobStatus.Executing)
        {
            job.MarkAsPreviewed();
            job.ConfirmMappings([]);
            job.MarkAsExecuting();
        }
        else if (status == ImportJobStatus.Completed)
        {
            job.MarkAsPreviewed();
            job.ConfirmMappings([]);
            job.MarkAsExecuting();
            job.Complete(ImportJobStatus.Completed, BuildReport(), DateTimeOffset.UtcNow);
        }
        else if (status == ImportJobStatus.Cancelled)
        {
            job.Cancel();
        }
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
