using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Granit.DataExchange.Endpoints.Dtos.Export;
using Granit.DataExchange.Endpoints.Dtos.Import;
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
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.Endpoints.Tests.Import;

/// <summary>
/// Integration tests for upload, preview, and mapping confirmation endpoints.
/// Uses a TestServer + NSubstitute mocks.
/// </summary>
public sealed class ImportUploadEndpointsTests : IAsyncDisposable
{
    private const string AdminRole = "granit-data-exchange-admin";
    private const string Prefix = "/data-exchange";

    private readonly IImportJobReader _jobReader = Substitute.For<IImportJobReader>();
    private readonly IImportJobWriter _jobWriter = Substitute.For<IImportJobWriter>();
    private readonly IImportFileProvider _fileProvider = Substitute.For<IImportFileProvider>();
    private readonly IMappingSuggestionService _mappingService = Substitute.For<IMappingSuggestionService>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly IImportDefinitionDescriptor _descriptor = Substitute.For<IImportDefinitionDescriptor>();
    private readonly IFileParser _parser = Substitute.For<IFileParser>();
    private readonly WebApplication _app;
    private readonly HttpClient _adminClient;
    private readonly HttpClient _userClient;
    private readonly HttpClient _anonClient;

    public ImportUploadEndpointsTests()
    {
        _clock.Now.Returns(DateTimeOffset.UtcNow);
        _descriptor.Name.Returns("Test.Import");
        _descriptor.EntityType.Returns(typeof(object));
        _descriptor.MaxFileSizeMb.Returns(10);
        _descriptor.AllowedMimeTypes.Returns(new[] { "text/csv" });
        _descriptor.GetFieldMetadata().Returns([
            new ImportFieldMetadata("Name", "String", "Name", null, true),
        ]);

        _parser.CanParse("text/csv").Returns(true);
        _parser.ExtractHeadersAsync(Arg.Any<Stream>(), Arg.Any<FileParsingOptions>(), Arg.Any<CancellationToken>())
            .Returns(new[] { "Name", "Email" });
        _parser.ReadPreviewAsync(Arg.Any<Stream>(), Arg.Any<FileParsingOptions>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new[] { new[] { "Alice", "alice@test.com" } });

        _fileProvider.SaveAsync(Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns("blob-ref-1");
        _fileProvider.OpenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes("Name,Email\nAlice,alice@test.com"))));

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();

        builder.Services
            .AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthHandler.SchemeName, _ => { });

        builder.Services.AddAuthorizationBuilder()
            .AddPolicy(DataExchangePermissions.Imports.Execute, policy => policy.RequireRole(AdminRole))
            .AddPolicy(DataExchangePermissions.Exports.Execute, policy => policy.RequireRole(AdminRole));
        builder.Services.AddSingleton(_jobReader);
        builder.Services.AddSingleton(_jobWriter);
        builder.Services.AddSingleton(_fileProvider);
        builder.Services.AddSingleton(_mappingService);
        builder.Services.AddSingleton(_clock);
        builder.Services.AddSingleton(_descriptor);
        builder.Services.AddSingleton<IFileParser>(_parser);
        builder.Services.AddSingleton<IGuidGenerator>(new SimpleGuidGenerator());

        // ImportUploadOrchestrator (extracted from handler)
        builder.Services.AddScoped<Granit.DataExchange.Endpoints.Internal.Import.ImportUploadOrchestrator>();

        // Required by export endpoints (all endpoints are compiled at startup)
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

    // ── POST / (Upload) ─────────────────────────────────────────────────────

    [Fact]
    public async Task Upload_WithValidFile_Returns201()
    {
        // Arrange
        using MultipartFormDataContent content = BuildMultipartContent("test.csv", "text/csv", "Name,Email\nAlice,alice@test.com");
        content.Add(new StringContent("Test.Import"), "definitionName");

        // Act
        HttpResponseMessage response = await _adminClient.PostAsync(Prefix, content, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        ImportJobResponse? result = await response.Content.ReadFromJsonAsync<ImportJobResponse>(TestContext.Current.CancellationToken);
        result.ShouldNotBeNull();
        result!.DefinitionName.ShouldBe("Test.Import");
        result.Status.ShouldBe(ImportJobStatus.Created);
        await _fileProvider.Received(1).SaveAsync(Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>());
        await _jobWriter.Received(1).CreateAsync(Arg.Any<ImportJob>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Upload_UnknownDefinition_Returns400()
    {
        // Arrange
        using MultipartFormDataContent content = BuildMultipartContent("test.csv", "text/csv", "data");
        content.Add(new StringContent("Unknown.Import"), "definitionName");

        // Act
        HttpResponseMessage response = await _adminClient.PostAsync(Prefix, content, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Upload_FileTooLarge_Returns400()
    {
        // Arrange — descriptor allows 10 MB, we send > 10 MB content type check
        _descriptor.MaxFileSizeMb.Returns(0); // 0 MB = reject everything

        using MultipartFormDataContent content = BuildMultipartContent("test.csv", "text/csv", "data");
        content.Add(new StringContent("Test.Import"), "definitionName");

        // Act
        HttpResponseMessage response = await _adminClient.PostAsync(Prefix, content, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Upload_InvalidMimeType_Returns400()
    {
        // Arrange
        using MultipartFormDataContent content = BuildMultipartContent("test.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "data");
        content.Add(new StringContent("Test.Import"), "definitionName");

        // Act
        HttpResponseMessage response = await _adminClient.PostAsync(Prefix, content, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Upload_WithoutAuth_Returns401()
    {
        // Arrange
        using MultipartFormDataContent content = BuildMultipartContent("test.csv", "text/csv", "data");
        content.Add(new StringContent("Test.Import"), "definitionName");

        // Act
        HttpResponseMessage response = await _anonClient.PostAsync(Prefix, content, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Upload_WithWrongRole_Returns403()
    {
        // Arrange
        using MultipartFormDataContent content = BuildMultipartContent("test.csv", "text/csv", "data");
        content.Add(new StringContent("Test.Import"), "definitionName");

        // Act
        HttpResponseMessage response = await _userClient.PostAsync(Prefix, content, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // ── POST /{jobId}/preview ───────────────────────────────────────────────

    [Fact]
    public async Task Preview_WhenJobExists_Returns200WithPreview()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        ImportJob job = BuildJob(jobId, ImportJobStatus.Created);
        _jobReader.GetAsync(jobId, Arg.Any<CancellationToken>()).Returns(job);

        // Act
        HttpResponseMessage response = await _adminClient.PostAsync(
            $"{Prefix}/{jobId}/preview", content: null, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        ImportPreviewResponse? result = await response.Content.ReadFromJsonAsync<ImportPreviewResponse>(TestContext.Current.CancellationToken);
        result.ShouldNotBeNull();
        result!.Headers.Count.ShouldBe(2);
        result.PreviewRows.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Preview_WhenJobNotFound_Returns404()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        _jobReader.GetAsync(jobId, Arg.Any<CancellationToken>()).Returns((ImportJob?)null);

        // Act
        HttpResponseMessage response = await _adminClient.PostAsync(
            $"{Prefix}/{jobId}/preview", content: null, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // ── PUT /{jobId}/mappings ───────────────────────────────────────────────

    [Fact]
    public async Task ConfirmMappings_WhenJobExists_Returns204()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        ImportJob job = BuildJob(jobId, ImportJobStatus.Previewed);
        _jobReader.GetAsync(jobId, Arg.Any<CancellationToken>()).Returns(job);

        ConfirmMappingsRequest request = new([
            new ImportColumnMapping("Name", "Name", MappingConfidence.Manual),
        ]);

        // Act
        HttpResponseMessage response = await _adminClient.PutAsJsonAsync(
            $"{Prefix}/{jobId}/mappings", request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await _jobWriter.Received(1).UpdateAsync(Arg.Is<ImportJob>(j => j.Status == ImportJobStatus.Mapped), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ConfirmMappings_WhenJobNotFound_Returns404()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        _jobReader.GetAsync(jobId, Arg.Any<CancellationToken>()).Returns((ImportJob?)null);
        ConfirmMappingsRequest request = new([new ImportColumnMapping("Name", "Name", MappingConfidence.Manual)]);

        // Act
        HttpResponseMessage response = await _adminClient.PutAsJsonAsync(
            $"{Prefix}/{jobId}/mappings", request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ConfirmMappings_EmptyMappings_Returns400()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        ImportJob job = BuildJob(jobId, ImportJobStatus.Previewed);
        _jobReader.GetAsync(jobId, Arg.Any<CancellationToken>()).Returns(job);
        ConfirmMappingsRequest request = new([]);

        // Act
        HttpResponseMessage response = await _adminClient.PutAsJsonAsync(
            $"{Prefix}/{jobId}/mappings", request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private HttpClient BuildClient(string role)
    {
        HttpClient client = _app.GetTestClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, role);
        return client;
    }

    private static MultipartFormDataContent BuildMultipartContent(string fileName, string mimeType, string data)
    {
        MultipartFormDataContent content = [];
        ByteArrayContent fileContent = new(Encoding.UTF8.GetBytes(data));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(mimeType);
        content.Add(fileContent, "file", fileName);
        return content;
    }

    private static ImportJob BuildJob(Guid id, ImportJobStatus status)
    {
        var job = ImportJob.Create(
            id,
            "Test.Import",
            "Object",
            "test.csv",
            "text/csv",
            100,
            "blob-ref-1");
        job.CreatedAt = DateTimeOffset.UtcNow;

        // Transition to desired status using behavior methods
        if (status == ImportJobStatus.Previewed)
        {
            job.MarkAsPreviewed();
        }
        else if (status == ImportJobStatus.Mapped)
        {
            job.MarkAsPreviewed();
            job.ConfirmMappings("[]");
        }
        else if (status == ImportJobStatus.Executing)
        {
            job.MarkAsExecuting();
        }

        return job;
    }
}
