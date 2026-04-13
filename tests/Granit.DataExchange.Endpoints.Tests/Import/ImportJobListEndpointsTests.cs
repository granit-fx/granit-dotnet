using System.Net;
using System.Net.Http.Json;
using Granit.DataExchange.Endpoints.Dtos.Import;
using Granit.DataExchange.Endpoints.Extensions;
using Granit.DataExchange.Endpoints.Permissions;
using Granit.DataExchange.Export;
using Granit.DataExchange.Import.Domain;
using Granit.DataExchange.Import.Mapping;
using Granit.DataExchange.Import.Parsing;
using Granit.DataExchange.Import.Pipeline;
using Granit.Guids;
using Granit.QueryEngine;
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
/// Integration tests for the import job listing endpoint (GET /jobs).
/// </summary>
public sealed class ImportJobListEndpointsTests : IAsyncDisposable
{
    private const string AdminRole = "granit-data-exchange-admin";
    private const string ImportPrefix = "/data-exchange/import";

    private readonly IImportJobReader _jobReader = Substitute.For<IImportJobReader>();
    private readonly WebApplication _app;
    private readonly HttpClient _adminClient;
    private readonly HttpClient _userClient;
    private readonly HttpClient _anonClient;

    public ImportJobListEndpointsTests()
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
        builder.Services.AddSingleton(Substitute.For<IImportJobWriter>());
        builder.Services.AddSingleton(Substitute.For<IImportFileProvider>());
        builder.Services.AddSingleton(Substitute.For<IMappingSuggestionService>());
        builder.Services.AddSingleton(Substitute.For<IClock>());
        builder.Services.AddSingleton(Substitute.For<IImportDefinitionDescriptor>());
        builder.Services.AddSingleton(Substitute.For<IFileParser>());
        builder.Services.AddSingleton<IGuidGenerator>(new SimpleGuidGenerator());

        // Required by export endpoints (compiled at startup)
        builder.Services.AddSingleton(Substitute.For<IExportOrchestrator>());
        builder.Services.AddSingleton(Substitute.For<IExportPresetReader>());
        builder.Services.AddSingleton(Substitute.For<IExportPresetWriter>());
        builder.Services.AddSingleton(Substitute.For<IExportJobReader>());

        _app = builder.Build();
        _app.MapGranitDataExchange();
        _app.StartAsync().GetAwaiter().GetResult();

        _adminClient = BuildClient(AdminRole);
        _userClient = BuildClient("regular-user");
        _anonClient = _app.GetTestClient();
    }

    public async ValueTask DisposeAsync() => await _app.DisposeAsync();

    // ── GET /jobs ────────────────────────────────────────────────────────

    [Fact]
    public async Task List_returns_200_with_paged_result()
    {
        // Arrange
        ImportJob job = CreateJob(ImportJobStatus.Completed);
        _jobReader.ListAsync(null, 1, 20, Arg.Any<CancellationToken>())
            .Returns(new PagedResult<ImportJob>([job], 1, HasMore: false));

        // Act
        HttpResponseMessage response = await _adminClient.GetAsync(
            $"{ImportPrefix}/jobs", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        PagedResult<ImportJobResponse>? result = await response.Content
            .ReadFromJsonAsync<PagedResult<ImportJobResponse>>(TestContext.Current.CancellationToken);
        result.ShouldNotBeNull();
        result!.TotalCount.ShouldBe(1);
        result.Items.Count.ShouldBe(1);
        result.Items[0].Id.ShouldBe(job.Id);
    }

    [Fact]
    public async Task List_with_status_filter_passes_status_to_store()
    {
        // Arrange
        _jobReader.ListAsync(ImportJobStatus.Failed, 1, 20, Arg.Any<CancellationToken>())
            .Returns(new PagedResult<ImportJob>([], 0, HasMore: false));

        // Act
        HttpResponseMessage response = await _adminClient.GetAsync(
            $"{ImportPrefix}/jobs?status=Failed", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await _jobReader.Received(1).ListAsync(
            ImportJobStatus.Failed, 1, 20, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task List_with_pagination_passes_page_and_pageSize()
    {
        // Arrange
        _jobReader.ListAsync(null, 2, 10, Arg.Any<CancellationToken>())
            .Returns(new PagedResult<ImportJob>([], 0, HasMore: false));

        // Act
        HttpResponseMessage response = await _adminClient.GetAsync(
            $"{ImportPrefix}/jobs?page=2&pageSize=10", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await _jobReader.Received(1).ListAsync(
            null, 2, 10, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task List_clamps_pageSize_to_100()
    {
        // Arrange
        _jobReader.ListAsync(null, 1, 100, Arg.Any<CancellationToken>())
            .Returns(new PagedResult<ImportJob>([], 0, HasMore: false));

        // Act
        HttpResponseMessage response = await _adminClient.GetAsync(
            $"{ImportPrefix}/jobs?pageSize=500", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await _jobReader.Received(1).ListAsync(
            null, 1, 100, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task List_clamps_page_to_minimum_1()
    {
        // Arrange
        _jobReader.ListAsync(null, 1, 20, Arg.Any<CancellationToken>())
            .Returns(new PagedResult<ImportJob>([], 0, HasMore: false));

        // Act
        HttpResponseMessage response = await _adminClient.GetAsync(
            $"{ImportPrefix}/jobs?page=-5", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await _jobReader.Received(1).ListAsync(
            null, 1, 20, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task List_empty_result_returns_200_with_zero_items()
    {
        // Arrange
        _jobReader.ListAsync(null, 1, 20, Arg.Any<CancellationToken>())
            .Returns(new PagedResult<ImportJob>([], 0, HasMore: false));

        // Act
        HttpResponseMessage response = await _adminClient.GetAsync(
            $"{ImportPrefix}/jobs", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        PagedResult<ImportJobResponse>? result = await response.Content
            .ReadFromJsonAsync<PagedResult<ImportJobResponse>>(TestContext.Current.CancellationToken);
        result.ShouldNotBeNull();
        result!.TotalCount.ShouldBe(0);
        result.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task List_without_auth_returns_401()
    {
        // Act
        HttpResponseMessage response = await _anonClient.GetAsync(
            $"{ImportPrefix}/jobs", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task List_wrong_role_returns_403()
    {
        // Act
        HttpResponseMessage response = await _userClient.GetAsync(
            $"{ImportPrefix}/jobs", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static ImportJob CreateJob(ImportJobStatus status)
    {
        var job = ImportJob.Create(
            Guid.NewGuid(), "Test.Import", "TestEntity", "test.csv", "text/csv", 1024, "blob-ref");

        if (status == ImportJobStatus.Completed)
        {
            job.MarkAsPreviewed();
            job.ConfirmMappings("[]");
            job.MarkAsExecuting();
            job.Complete(ImportJobStatus.Completed, "{}", DateTimeOffset.UtcNow);
        }
        else if (status == ImportJobStatus.Executing)
        {
            job.MarkAsPreviewed();
            job.ConfirmMappings("[]");
            job.MarkAsExecuting();
        }

        return job;
    }

    private HttpClient BuildClient(string role)
    {
        HttpClient client = _app.GetTestClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, role);
        return client;
    }
}
