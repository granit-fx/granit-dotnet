using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using FluentValidation;
using Granit.Exceptions;
using Granit.Templating.Endpoints.Dtos;
using Granit.Templating.Endpoints.Extensions;
using Granit.Templating.Endpoints.Permissions;
using Granit.Templating.Endpoints.Validators;
using Granit.Templating.Exceptions;
using Granit.Templating.GlobalContext;
using Granit.Templating.Keys;
using Granit.Templating.Pipeline;
using Granit.Templating.Store;
using Granit.Users;
using Granit.Workflow.Domain;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Xunit;

namespace Granit.Templating.Endpoints.Tests;

/// <summary>
/// Integration tests for the template administration endpoints
/// (GET/POST/PUT/DELETE). Uses a TestServer + NSubstitute mocks for
/// <see cref="IDocumentTemplateStoreReader"/> and <see cref="IDocumentTemplateStoreWriter"/>.
/// </summary>
public sealed class TemplatingEndpointsTests : IAsyncDisposable
{
    private const string Prefix = "/templating/templates";
    private const string AnyStamp = "00000000-0000-0000-0000-000000000000";

    private static readonly string[] AllPermissions =
    [
        TemplatingPermissions.Templates.Read,
        TemplatingPermissions.Templates.Manage,
        TemplatingPermissions.Categories.Read,
        TemplatingPermissions.Categories.Manage,
    ];

    private readonly IDocumentTemplateStoreReader _storeReader = Substitute.For<IDocumentTemplateStoreReader>();
    private readonly IDocumentTemplateStoreWriter _storeWriter = Substitute.For<IDocumentTemplateStoreWriter>();
    private readonly ITemplateTransitionHook _transitionHook = Substitute.For<ITemplateTransitionHook>();
    private readonly WebApplication _app;
    private readonly HttpClient _adminClient;
    private readonly HttpClient _anonClient;

    public TemplatingEndpointsTests()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services
            .AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthHandler.SchemeName, _ => { });

        builder.Services.AddAuthorizationBuilder()
            .AddPolicy(TemplatingPermissions.Templates.Read,
                policy => policy.RequireClaim(TestAuthHandler.PermissionClaimType, TemplatingPermissions.Templates.Read))
            .AddPolicy(TemplatingPermissions.Templates.Manage,
                policy => policy.RequireClaim(TestAuthHandler.PermissionClaimType, TemplatingPermissions.Templates.Manage))
            .AddPolicy(TemplatingPermissions.Categories.Read,
                policy => policy.RequireClaim(TestAuthHandler.PermissionClaimType, TemplatingPermissions.Categories.Read))
            .AddPolicy(TemplatingPermissions.Categories.Manage,
                policy => policy.RequireClaim(TestAuthHandler.PermissionClaimType, TemplatingPermissions.Categories.Manage));

        builder.Services.AddSingleton(_storeReader);
        builder.Services.AddSingleton(_storeWriter);
        builder.Services.AddSingleton(_transitionHook);
        builder.Services.AddSingleton<IValidator<SaveTemplateRequest>, SaveTemplateRequestValidator>();
        builder.Services.AddSingleton(CreateTestUserService());

        _app = builder.Build();
        _app.MapGranitTemplating();
        _app.StartAsync().GetAwaiter().GetResult();

        _adminClient = BuildClient(AllPermissions);
        _anonClient = _app.GetTestClient();
    }

    public async ValueTask DisposeAsync()
    {
        _adminClient.Dispose();
        _anonClient.Dispose();
        await _app.DisposeAsync();
    }

    // =========================================================================
    // GET / + GET /meta — paginated list + query metadata
    // (provided by MapGranitQuery<TemplateSummary>; backed by IQueryableSource<TemplateSummary>
    //  registered by Granit.Templating.EntityFrameworkCore — exercised in the EF integration
    //  test suite rather than here, so this file no longer mocks the list contract.)
    // =========================================================================

    // =========================================================================
    // GET /{name} — Template detail
    // =========================================================================

    [Fact]
    public async Task GetDetail_WhenStoreNotRegistered_Returns501()
    {
        await using WebApplication app = await BuildAppWithoutStoreAsync();
        using HttpClient client = BuildClient(app, AllPermissions);

        HttpResponseMessage response = await client.GetAsync(
            $"{Prefix}/Billing.Invoice",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotImplemented);
    }

    [Fact]
    public async Task GetDetail_WhenNotFound_Returns404()
    {
        _storeReader.TryGetDraftAsync(Arg.Any<TemplateKey>(), Arg.Any<CancellationToken>())
            .Returns((TemplateRevision?)null);
        _storeReader.TryGetPublishedAsync(Arg.Any<TemplateKey>(), Arg.Any<CancellationToken>())
            .Returns((TemplateDescriptor?)null);

        HttpResponseMessage response = await _adminClient.GetAsync(
            $"{Prefix}/Billing.Invoice",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetDetail_WithDraftOnly_Returns200()
    {
        var draft = new TemplateRevision
        {
            RevisionId = Guid.NewGuid(),
            Content = "<h1>Draft</h1>",
            MimeType = "text/html",
            Status = WorkflowLifecycleStatus.Draft,
            Version = 1,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = "user-1",
            ConcurrencyStamp = AnyStamp,
        };
        _storeReader.TryGetDraftAsync(
                Arg.Is<TemplateKey>(k => k.Name == "Billing.Invoice"), Arg.Any<CancellationToken>())
            .Returns(draft);
        _storeReader.TryGetPublishedAsync(Arg.Any<TemplateKey>(), Arg.Any<CancellationToken>())
            .Returns((TemplateDescriptor?)null);

        HttpResponseMessage response = await _adminClient.GetAsync(
            $"{Prefix}/Billing.Invoice",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        TemplateDetailResponse? result =
            await response.Content.ReadFromJsonAsync<TemplateDetailResponse>(
                TestContext.Current.CancellationToken);
        result.ShouldNotBeNull();
        result.Name.ShouldBe("Billing.Invoice");
        result.Draft.ShouldNotBeNull();
        result.Draft.Content.ShouldBe("<h1>Draft</h1>");
        result.Published.ShouldBeNull();
    }

    [Fact]
    public async Task GetDetail_WithPublishedRevision_Returns200WithBoth()
    {
        var draft = new TemplateRevision
        {
            RevisionId = Guid.NewGuid(),
            Content = "<h1>Draft v2</h1>",
            MimeType = "text/html",
            Status = WorkflowLifecycleStatus.Draft,
            Version = 1,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = "user-1",
            ConcurrencyStamp = AnyStamp,
        };
        var publishedRevision = new TemplateRevision
        {
            RevisionId = Guid.NewGuid(),
            Content = "<h1>Published</h1>",
            MimeType = "text/html",
            Status = WorkflowLifecycleStatus.Published,
            Version = 1,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
            CreatedBy = "user-1",
            PublishedAt = DateTimeOffset.UtcNow.AddHours(-1),
            PublishedBy = "user-1",
            ConcurrencyStamp = AnyStamp,
        };
        var descriptor = new TemplateDescriptor
        {
            Content = "<h1>Published</h1>",
            MimeType = "text/html",
            RevisionId = publishedRevision.RevisionId,
        };

        _storeReader.TryGetDraftAsync(Arg.Any<TemplateKey>(), Arg.Any<CancellationToken>())
            .Returns(draft);
        _storeReader.TryGetPublishedAsync(Arg.Any<TemplateKey>(), Arg.Any<CancellationToken>())
            .Returns(descriptor);
        _storeReader.GetHistoryAsync(Arg.Any<TemplateKey>(), Arg.Any<CancellationToken>())
            .Returns([draft, publishedRevision]);

        HttpResponseMessage response = await _adminClient.GetAsync(
            $"{Prefix}/Billing.Invoice",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        TemplateDetailResponse? result =
            await response.Content.ReadFromJsonAsync<TemplateDetailResponse>(
                TestContext.Current.CancellationToken);
        result.ShouldNotBeNull();
        result.Draft.ShouldNotBeNull();
        result.Published.ShouldNotBeNull();
        result.Published.Content.ShouldBe("<h1>Published</h1>");
    }

    [Fact]
    public async Task GetDetail_WithInvalidName_Returns400()
    {
        HttpResponseMessage response = await _adminClient.GetAsync(
            $"{Prefix}/invalid-name",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetDetail_WithInvalidCulture_Returns400()
    {
        HttpResponseMessage response = await _adminClient.GetAsync(
            $"{Prefix}/Billing.Invoice?culture=en:bad",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // =========================================================================
    // POST / — Create draft
    // =========================================================================

    [Fact]
    public async Task CreateDraft_WhenStoreNotRegistered_Returns501()
    {
        await using WebApplication app = await BuildAppWithoutStoreAsync();
        using HttpClient client = BuildClient(app, AllPermissions);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            Prefix,
            new SaveTemplateRequest(Content: "<h1>Hello</h1>", Name: "Billing.Invoice", Culture: null),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotImplemented);
    }

    [Fact]
    public async Task CreateDraft_WithValidRequest_Returns201()
    {
        var createdDraft = new TemplateRevision
        {
            RevisionId = Guid.NewGuid(),
            Content = "<h1>Hello</h1>",
            MimeType = "text/html",
            Status = WorkflowLifecycleStatus.Draft,
            Version = 1,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = "test-user",
            ConcurrencyStamp = AnyStamp,
        };
        _storeReader.TryGetDraftAsync(Arg.Any<TemplateKey>(), Arg.Any<CancellationToken>())
            .Returns(createdDraft);
        _storeReader.TryGetPublishedAsync(Arg.Any<TemplateKey>(), Arg.Any<CancellationToken>())
            .Returns((TemplateDescriptor?)null);

        HttpResponseMessage response = await _adminClient.PostAsJsonAsync(
            Prefix,
            new SaveTemplateRequest(Content: "<h1>Hello</h1>", Name: "Billing.Invoice", Culture: "fr"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        TemplateDetailResponse? result =
            await response.Content.ReadFromJsonAsync<TemplateDetailResponse>(
                TestContext.Current.CancellationToken);
        result.ShouldNotBeNull();
        result.Name.ShouldBe("Billing.Invoice");
        result.Draft.ShouldNotBeNull();
        result.Draft.Content.ShouldBe("<h1>Hello</h1>");

        await _storeWriter.Received(1).SaveDraftAsync(
            Arg.Is<TemplateKey>(k => k.Name == "Billing.Invoice" && k.Culture == "fr"),
            "<h1>Hello</h1>",
            "text/html",
            Arg.Any<string>(),
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateDraft_WithoutName_Returns400()
    {
        HttpResponseMessage response = await _adminClient.PostAsJsonAsync(
            Prefix,
            new SaveTemplateRequest(Content: "<h1>Hello</h1>", Name: null, Culture: null),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateDraft_WithInvalidName_Returns422()
    {
        HttpResponseMessage response = await _adminClient.PostAsJsonAsync(
            Prefix,
            new SaveTemplateRequest(Content: "<h1>Hello</h1>", Name: "invalid", Culture: null),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task CreateDraft_WithEmptyContent_Returns422()
    {
        HttpResponseMessage response = await _adminClient.PostAsJsonAsync(
            Prefix,
            new SaveTemplateRequest(Content: "", Name: "Billing.Invoice", Culture: null),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    // =========================================================================
    // PUT /{name} — Update draft
    // =========================================================================

    [Fact]
    public async Task UpdateDraft_WhenStoreNotRegistered_Returns501()
    {
        await using WebApplication app = await BuildAppWithoutStoreAsync();
        using HttpClient client = BuildClient(app, AllPermissions);

        HttpResponseMessage response = await client.PutAsJsonAsync(
            $"{Prefix}/Billing.Invoice",
            new SaveTemplateRequest(Content: "<h1>Updated</h1>", Name: null, Culture: null),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotImplemented);
    }

    [Fact]
    public async Task UpdateDraft_WithValidRequest_Returns200()
    {
        var updatedDraft = new TemplateRevision
        {
            RevisionId = Guid.NewGuid(),
            Content = "<h1>Updated</h1>",
            MimeType = "text/html",
            Status = WorkflowLifecycleStatus.Draft,
            Version = 1,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = "test-user",
            ConcurrencyStamp = AnyStamp,
        };
        _storeReader.TryGetDraftAsync(Arg.Any<TemplateKey>(), Arg.Any<CancellationToken>())
            .Returns(updatedDraft);
        _storeReader.TryGetPublishedAsync(Arg.Any<TemplateKey>(), Arg.Any<CancellationToken>())
            .Returns((TemplateDescriptor?)null);

        HttpResponseMessage response = await _adminClient.PutAsJsonAsync(
            $"{Prefix}/Billing.Invoice",
            new SaveTemplateRequest(Content: "<h1>Updated</h1>", Name: null, Culture: "fr"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        TemplateDetailResponse? result =
            await response.Content.ReadFromJsonAsync<TemplateDetailResponse>(
                TestContext.Current.CancellationToken);
        result.ShouldNotBeNull();
        result.Name.ShouldBe("Billing.Invoice");
        result.Draft.ShouldNotBeNull();
        result.Draft.Content.ShouldBe("<h1>Updated</h1>");

        await _storeWriter.Received(1).SaveDraftAsync(
            Arg.Is<TemplateKey>(k => k.Name == "Billing.Invoice" && k.Culture == "fr"),
            "<h1>Updated</h1>",
            "text/html",
            Arg.Any<string>(),
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateDraft_WithInvalidName_Returns400()
    {
        HttpResponseMessage response = await _adminClient.PutAsJsonAsync(
            $"{Prefix}/bad",
            new SaveTemplateRequest(Content: "<h1>Updated</h1>", Name: null, Culture: null),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // =========================================================================
    // DELETE /{name}/draft — Delete draft
    // =========================================================================

    [Fact]
    public async Task DeleteDraft_WhenStoreNotRegistered_Returns501()
    {
        await using WebApplication app = await BuildAppWithoutStoreAsync();
        using HttpClient client = BuildClient(app, AllPermissions);

        HttpResponseMessage response = await client.DeleteAsync(
            $"{Prefix}/Billing.Invoice/draft",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotImplemented);
    }

    [Fact]
    public async Task DeleteDraft_WithValidRequest_Returns204()
    {
        _storeWriter.DeleteDraftAsync(Arg.Any<TemplateKey>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        HttpResponseMessage response = await _adminClient.DeleteAsync(
            $"{Prefix}/Billing.Invoice/draft",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await _storeWriter.Received(1).DeleteDraftAsync(
            Arg.Is<TemplateKey>(k => k.Name == "Billing.Invoice"),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteDraft_WhenNoDraftExists_Returns404()
    {
        _storeWriter.DeleteDraftAsync(Arg.Any<TemplateKey>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new NotFoundException("No draft exists for this key."));

        HttpResponseMessage response = await _adminClient.DeleteAsync(
            $"{Prefix}/Billing.Invoice/draft",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteDraft_WithInvalidName_Returns400()
    {
        HttpResponseMessage response = await _adminClient.DeleteAsync(
            $"{Prefix}/bad/draft",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DeleteDraft_WithInvalidCulture_Returns400()
    {
        HttpResponseMessage response = await _adminClient.DeleteAsync(
            $"{Prefix}/Billing.Invoice/draft?culture=en:bad",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // =========================================================================
    // POST /{name}/publish — Publish
    // =========================================================================

    [Fact]
    public async Task Publish_WhenStoreNotRegistered_Returns501()
    {
        await using WebApplication app = await BuildAppWithoutStoreAsync();
        using HttpClient client = BuildClient(app, AllPermissions);

        HttpResponseMessage response = await client.PostAsync(
            $"{Prefix}/Billing.Invoice/publish",
            null,
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotImplemented);
    }

    [Fact]
    public async Task Publish_WithValidDraft_Returns200()
    {
        var publishedRevision = new TemplateRevision
        {
            RevisionId = Guid.NewGuid(),
            Content = "<h1>Published</h1>",
            MimeType = "text/html",
            Status = WorkflowLifecycleStatus.Published,
            Version = 1,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = "test-user",
            PublishedAt = DateTimeOffset.UtcNow,
            PublishedBy = "test-user",
            ConcurrencyStamp = AnyStamp,
        };

        _storeWriter.PublishAsync(Arg.Any<TemplateKey>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _storeReader.TryGetDraftAsync(Arg.Any<TemplateKey>(), Arg.Any<CancellationToken>())
            .Returns((TemplateRevision?)null);
        _storeReader.TryGetPublishedAsync(Arg.Any<TemplateKey>(), Arg.Any<CancellationToken>())
            .Returns(new TemplateDescriptor { Content = "<h1>Published</h1>", MimeType = "text/html", RevisionId = publishedRevision.RevisionId });
        _storeReader.GetHistoryAsync(Arg.Any<TemplateKey>(), Arg.Any<CancellationToken>())
            .Returns([publishedRevision]);

        HttpResponseMessage response = await _adminClient.PostAsync(
            $"{Prefix}/Billing.Invoice/publish",
            null,
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        TemplateDetailResponse? result =
            await response.Content.ReadFromJsonAsync<TemplateDetailResponse>(
                TestContext.Current.CancellationToken);
        result.ShouldNotBeNull();
        result.Published.ShouldNotBeNull();

        await _storeWriter.Received(1).PublishAsync(
            Arg.Is<TemplateKey>(k => k.Name == "Billing.Invoice"),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Publish_WhenNoDraftExists_Returns404()
    {
        _storeWriter.PublishAsync(Arg.Any<TemplateKey>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new NotFoundException("Cannot publish: no draft exists."));

        HttpResponseMessage response = await _adminClient.PostAsync(
            $"{Prefix}/Billing.Invoice/publish",
            null,
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Publish_WhenTransitionDenied_Returns409()
    {
        _storeWriter.PublishAsync(Arg.Any<TemplateKey>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new TemplateTransitionDeniedException(
                WorkflowLifecycleStatus.Draft, WorkflowLifecycleStatus.Published));

        HttpResponseMessage response = await _adminClient.PostAsync(
            $"{Prefix}/Billing.Invoice/publish",
            null,
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Publish_WithInvalidName_Returns400()
    {
        HttpResponseMessage response = await _adminClient.PostAsync(
            $"{Prefix}/bad/publish",
            null,
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // =========================================================================
    // POST /{name}/unpublish — Unpublish
    // =========================================================================

    [Fact]
    public async Task Unpublish_WhenStoreNotRegistered_Returns501()
    {
        await using WebApplication app = await BuildAppWithoutStoreAsync();
        using HttpClient client = BuildClient(app, AllPermissions);

        HttpResponseMessage response = await client.PostAsync(
            $"{Prefix}/Billing.Invoice/unpublish",
            null,
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotImplemented);
    }

    [Fact]
    public async Task Unpublish_WithValidRequest_Returns204()
    {
        _storeWriter.UnpublishAsync(Arg.Any<TemplateKey>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        HttpResponseMessage response = await _adminClient.PostAsync(
            $"{Prefix}/Billing.Invoice/unpublish",
            null,
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await _storeWriter.Received(1).UnpublishAsync(
            Arg.Is<TemplateKey>(k => k.Name == "Billing.Invoice"),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Unpublish_WhenTransitionDenied_Returns409()
    {
        _storeWriter.UnpublishAsync(Arg.Any<TemplateKey>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new TemplateTransitionDeniedException(
                WorkflowLifecycleStatus.Published, WorkflowLifecycleStatus.Archived));

        HttpResponseMessage response = await _adminClient.PostAsync(
            $"{Prefix}/Billing.Invoice/unpublish",
            null,
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Unpublish_WithInvalidName_Returns400()
    {
        HttpResponseMessage response = await _adminClient.PostAsync(
            $"{Prefix}/bad/unpublish",
            null,
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // =========================================================================
    // GET /{name}/lifecycle — Lifecycle info
    // =========================================================================

    [Fact]
    public async Task GetLifecycle_WhenStoreNotRegistered_Returns501()
    {
        await using WebApplication app = await BuildAppWithoutStoreAsync();
        using HttpClient client = BuildClient(app, AllPermissions);

        HttpResponseMessage response = await client.GetAsync(
            $"{Prefix}/Billing.Invoice/lifecycle",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotImplemented);
    }

    [Fact]
    public async Task GetLifecycle_WhenNotFound_Returns404()
    {
        _storeReader.TryGetDraftAsync(Arg.Any<TemplateKey>(), Arg.Any<CancellationToken>())
            .Returns((TemplateRevision?)null);
        _storeReader.TryGetPublishedAsync(Arg.Any<TemplateKey>(), Arg.Any<CancellationToken>())
            .Returns((TemplateDescriptor?)null);

        HttpResponseMessage response = await _adminClient.GetAsync(
            $"{Prefix}/Billing.Invoice/lifecycle",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetLifecycle_WithDraft_ReturnsStatusAndTransitions()
    {
        var draft = new TemplateRevision
        {
            RevisionId = Guid.NewGuid(),
            Content = "<h1>Draft</h1>",
            MimeType = "text/html",
            Status = WorkflowLifecycleStatus.Draft,
            Version = 1,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = "user-1",
            ConcurrencyStamp = AnyStamp,
        };
        _storeReader.TryGetDraftAsync(Arg.Any<TemplateKey>(), Arg.Any<CancellationToken>())
            .Returns(draft);
        _storeReader.TryGetPublishedAsync(Arg.Any<TemplateKey>(), Arg.Any<CancellationToken>())
            .Returns((TemplateDescriptor?)null);

        _transitionHook.IsWorkflowEnabled.Returns(false);
        _transitionHook.CanTransitionAsync(
                Arg.Any<WorkflowLifecycleStatus>(), Arg.Any<WorkflowLifecycleStatus>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                WorkflowLifecycleStatus from = callInfo.ArgAt<WorkflowLifecycleStatus>(0);
                WorkflowLifecycleStatus target = callInfo.ArgAt<WorkflowLifecycleStatus>(1);
                return from == WorkflowLifecycleStatus.Draft && target == WorkflowLifecycleStatus.Published;
            });

        HttpResponseMessage response = await _adminClient.GetAsync(
            $"{Prefix}/Billing.Invoice/lifecycle",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        TemplateLifecycleResponse? result =
            await response.Content.ReadFromJsonAsync<TemplateLifecycleResponse>(
                TestContext.Current.CancellationToken);
        result.ShouldNotBeNull();
        result.Name.ShouldBe("Billing.Invoice");
        result.CurrentStatus.ShouldBe(WorkflowLifecycleStatus.Draft);
        result.WorkflowEnabled.ShouldBeFalse();
        result.AvailableTransitions.ShouldContain(WorkflowLifecycleStatus.Published);
    }

    [Fact]
    public async Task GetLifecycle_WithInvalidName_Returns400()
    {
        HttpResponseMessage response = await _adminClient.GetAsync(
            $"{Prefix}/bad/lifecycle",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // =========================================================================
    // GET /{name}/history — Revision history
    // =========================================================================

    [Fact]
    public async Task GetHistory_WhenStoreNotRegistered_Returns501()
    {
        await using WebApplication app = await BuildAppWithoutStoreAsync();
        using HttpClient client = BuildClient(app, AllPermissions);

        HttpResponseMessage response = await client.GetAsync(
            $"{Prefix}/Billing.Invoice/history",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotImplemented);
    }

    [Fact]
    public async Task GetHistory_WithEmptyHistory_Returns200()
    {
        _storeReader.GetHistoryAsync(Arg.Any<TemplateKey>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<TemplateRevision>)[]);

        HttpResponseMessage response = await _adminClient.GetAsync(
            $"{Prefix}/Billing.Invoice/history",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        TemplateHistoryResponse? result =
            await response.Content.ReadFromJsonAsync<TemplateHistoryResponse>(
                TestContext.Current.CancellationToken);
        result.ShouldNotBeNull();
        result.Revisions.ShouldBeEmpty();
        result.TotalCount.ShouldBe(0);
        result.Page.ShouldBe(1);
        result.PageSize.ShouldBe(20);
    }

    [Fact]
    public async Task GetHistory_WithRevisions_ReturnsSummariesWithoutContent()
    {
        var revisions = new List<TemplateRevision>
        {
            new()
            {
                RevisionId = Guid.NewGuid(),
                Content = "<h1>Published</h1>",
                MimeType = "text/html",
                Status = WorkflowLifecycleStatus.Published,
                Version = 1,
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedBy = "user-1",
                PublishedAt = DateTimeOffset.UtcNow,
                PublishedBy = "user-1",
                ConcurrencyStamp = AnyStamp,
            },
            new()
            {
                RevisionId = Guid.NewGuid(),
                Content = "<h1>Archived old</h1>",
                MimeType = "text/html",
                Status = WorkflowLifecycleStatus.Archived,
                Version = 1,
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-7),
                CreatedBy = "user-1",
                ConcurrencyStamp = AnyStamp,
            },
        };
        _storeReader.GetHistoryAsync(Arg.Any<TemplateKey>(), Arg.Any<CancellationToken>())
            .Returns(revisions);

        HttpResponseMessage response = await _adminClient.GetAsync(
            $"{Prefix}/Billing.Invoice/history",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        TemplateHistoryResponse? result =
            await response.Content.ReadFromJsonAsync<TemplateHistoryResponse>(
                TestContext.Current.CancellationToken);
        result.ShouldNotBeNull();
        result.TotalCount.ShouldBe(2);
        result.Revisions.Count.ShouldBe(2);
        result.Revisions[0].ContentLength.ShouldBe("<h1>Published</h1>".Length);
    }

    [Fact]
    public async Task GetHistory_WithPagination_ReturnsCorrectPage()
    {
        var revisions = Enumerable.Range(0, 15)
            .Select(i => new TemplateRevision
            {
                RevisionId = Guid.NewGuid(),
                Content = $"<p>Rev {i}</p>",
                MimeType = "text/html",
                Status = WorkflowLifecycleStatus.Archived,
                Version = 1,
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-i),
                CreatedBy = "user-1",
                ConcurrencyStamp = AnyStamp,
            })
            .ToList();
        _storeReader.GetHistoryAsync(Arg.Any<TemplateKey>(), Arg.Any<CancellationToken>())
            .Returns(revisions);

        HttpResponseMessage response = await _adminClient.GetAsync(
            $"{Prefix}/Billing.Invoice/history?page=2&pageSize=5",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        TemplateHistoryResponse? result =
            await response.Content.ReadFromJsonAsync<TemplateHistoryResponse>(
                TestContext.Current.CancellationToken);
        result.ShouldNotBeNull();
        result.TotalCount.ShouldBe(15);
        result.Revisions.Count.ShouldBe(5);
        result.Page.ShouldBe(2);
        result.PageSize.ShouldBe(5);
    }

    [Fact]
    public async Task GetHistory_WithInvalidName_Returns400()
    {
        HttpResponseMessage response = await _adminClient.GetAsync(
            $"{Prefix}/bad/history",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetHistory_WithInvalidPagination_Returns400()
    {
        HttpResponseMessage response = await _adminClient.GetAsync(
            $"{Prefix}/Billing.Invoice/history?page=0",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // =========================================================================
    // GET /{name}/history/{revisionId} — Revision detail
    // =========================================================================

    [Fact]
    public async Task GetRevisionDetail_WhenStoreNotRegistered_Returns501()
    {
        await using WebApplication app = await BuildAppWithoutStoreAsync();
        using HttpClient client = BuildClient(app, AllPermissions);

        HttpResponseMessage response = await client.GetAsync(
            $"{Prefix}/Billing.Invoice/history/{Guid.NewGuid()}",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotImplemented);
    }

    [Fact]
    public async Task GetRevisionDetail_WhenFound_Returns200WithContent()
    {
        var revisionId = Guid.NewGuid();
        var revision = new TemplateRevision
        {
            RevisionId = revisionId,
            Content = "<h1>Full content</h1>",
            MimeType = "text/html",
            Status = WorkflowLifecycleStatus.Archived,
            Version = 1,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = "user-1",
            ConcurrencyStamp = AnyStamp,
        };
        _storeReader.GetHistoryAsync(Arg.Any<TemplateKey>(), Arg.Any<CancellationToken>())
            .Returns([revision]);

        HttpResponseMessage response = await _adminClient.GetAsync(
            $"{Prefix}/Billing.Invoice/history/{revisionId}",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        TemplateRevisionResponse? result =
            await response.Content.ReadFromJsonAsync<TemplateRevisionResponse>(
                TestContext.Current.CancellationToken);
        result.ShouldNotBeNull();
        result.RevisionId.ShouldBe(revisionId);
        result.Content.ShouldBe("<h1>Full content</h1>");
    }

    [Fact]
    public async Task GetRevisionDetail_WhenNotFound_Returns404()
    {
        _storeReader.GetHistoryAsync(Arg.Any<TemplateKey>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<TemplateRevision>)[]);

        HttpResponseMessage response = await _adminClient.GetAsync(
            $"{Prefix}/Billing.Invoice/history/{Guid.NewGuid()}",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetRevisionDetail_WithInvalidName_Returns400()
    {
        HttpResponseMessage response = await _adminClient.GetAsync(
            $"{Prefix}/bad/history/{Guid.NewGuid()}",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // =========================================================================
    // Security tests
    // =========================================================================

    [Fact]
    public async Task ListTemplates_WithoutToken_Returns401()
    {
        HttpResponseMessage response = await _anonClient.GetAsync(
            Prefix,
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateDraft_WithoutToken_Returns401()
    {
        HttpResponseMessage response = await _anonClient.PostAsJsonAsync(
            Prefix,
            new SaveTemplateRequest(Content: "<h1>Hello</h1>", Name: "Billing.Invoice", Culture: null),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteDraft_WithoutToken_Returns401()
    {
        HttpResponseMessage response = await _anonClient.DeleteAsync(
            $"{Prefix}/Billing.Invoice/draft",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // =========================================================================
    // Route prefix tests
    // =========================================================================

    [Fact]
    public async Task MapGranitTemplating_WithCustomPrefix_RespondsOnCustomRoute()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services
            .AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthHandler.SchemeName, _ => { });
        builder.Services.AddAuthorizationBuilder()
            .AddPolicy(TemplatingPermissions.Templates.Read,
                policy => policy.RequireClaim(TestAuthHandler.PermissionClaimType, TemplatingPermissions.Templates.Read))
            .AddPolicy(TemplatingPermissions.Templates.Manage,
                policy => policy.RequireClaim(TestAuthHandler.PermissionClaimType, TemplatingPermissions.Templates.Manage))
            .AddPolicy(TemplatingPermissions.Categories.Read,
                policy => policy.RequireClaim(TestAuthHandler.PermissionClaimType, TemplatingPermissions.Categories.Read))
            .AddPolicy(TemplatingPermissions.Categories.Manage,
                policy => policy.RequireClaim(TestAuthHandler.PermissionClaimType, TemplatingPermissions.Categories.Manage));
        builder.Services.AddSingleton(_storeReader);
        builder.Services.AddSingleton(_storeWriter);
        builder.Services.AddSingleton<IValidator<SaveTemplateRequest>, SaveTemplateRequestValidator>();
        builder.Services.AddSingleton(CreateTestUserService());

        await using WebApplication app = builder.Build();
        app.MapGranitTemplating(opts => opts.RoutePrefix = "custom-templates");
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = BuildClient(app, AllPermissions);

        // Probe a route that doesn't depend on the Query Engine wiring (variables endpoint
        // returns 200 unconditionally for a valid template name).
        HttpResponseMessage notFound = await client.GetAsync(
            "/templating/templates/Billing.Invoice/variables",
            TestContext.Current.CancellationToken);

        HttpResponseMessage ok = await client.GetAsync(
            "/custom-templates/templates/Billing.Invoice/variables",
            TestContext.Current.CancellationToken);

        notFound.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        ok.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // =========================================================================
    // POST /{name}/preview — Preview a template draft
    // =========================================================================

    [Fact]
    public async Task Preview_WhenStoreNotRegistered_Returns501()
    {
        await using WebApplication app = await BuildAppWithoutStoreAsync();
        using HttpClient client = BuildClient(app, AllPermissions);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"{Prefix}/Billing.Invoice/preview",
            new TemplatePreviewRequest(null, null),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotImplemented);
    }

    [Fact]
    public async Task Preview_WhenNoDraft_Returns404()
    {
        _storeReader.TryGetDraftAsync(Arg.Any<TemplateKey>(), Arg.Any<CancellationToken>())
            .Returns((TemplateRevision?)null);

        HttpResponseMessage response = await _adminClient.PostAsJsonAsync(
            $"{Prefix}/Billing.Invoice/preview",
            new TemplatePreviewRequest(null, null),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Preview_WhenNoEngineRegistered_Returns501()
    {
        _storeReader.TryGetDraftAsync(Arg.Any<TemplateKey>(), Arg.Any<CancellationToken>())
            .Returns(new TemplateRevision
            {
                RevisionId = Guid.NewGuid(),
                Content = "<h1>Hello</h1>",
                MimeType = "text/html",
                Status = WorkflowLifecycleStatus.Draft,
                Version = 1,
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedBy = "admin",
                ConcurrencyStamp = AnyStamp,
            });

        // Default test app has no ITemplateEngine registered
        HttpResponseMessage response = await _adminClient.PostAsJsonAsync(
            $"{Prefix}/Billing.Invoice/preview",
            new TemplatePreviewRequest(null, null),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotImplemented);
    }

    [Fact]
    public async Task Preview_WithEngine_ReturnsRenderedHtml()
    {
        var revisionId = Guid.NewGuid();
        _storeReader.TryGetDraftAsync(Arg.Any<TemplateKey>(), Arg.Any<CancellationToken>())
            .Returns(new TemplateRevision
            {
                RevisionId = revisionId,
                Content = "<h1>Hello {{ name }}</h1>",
                MimeType = "text/html",
                Status = WorkflowLifecycleStatus.Draft,
                Version = 1,
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedBy = "admin",
                ConcurrencyStamp = AnyStamp,
            });

        await using WebApplication app = await BuildAppWithEngineAsync(
            _storeReader, _storeWriter, _transitionHook,
            new TextRenderedContent("<h1>Hello World</h1>", DocumentFormat.Html) { RevisionId = revisionId });
        using HttpClient client = BuildClient(app, AllPermissions);

        JsonElement data = JsonSerializer.Deserialize<JsonElement>("""{"name": "World"}""");
        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"{Prefix}/Billing.Invoice/preview",
            new TemplatePreviewRequest(null, data),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        TemplatePreviewResponse? result =
            await response.Content.ReadFromJsonAsync<TemplatePreviewResponse>(
                TestContext.Current.CancellationToken);
        result.ShouldNotBeNull();
        result.Html.ShouldBe("<h1>Hello World</h1>");
        result.RevisionId.ShouldBe(revisionId);
        result.RenderTimeMs.ShouldBeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task Preview_WithCulture_PassesCultureToStore()
    {
        var revisionId = Guid.NewGuid();
        _storeReader.TryGetDraftAsync(Arg.Any<TemplateKey>(), Arg.Any<CancellationToken>())
            .Returns(new TemplateRevision
            {
                RevisionId = revisionId,
                Content = "<p>Bonjour</p>",
                MimeType = "text/html",
                Status = WorkflowLifecycleStatus.Draft,
                Version = 1,
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedBy = "admin",
                ConcurrencyStamp = AnyStamp,
            });

        await using WebApplication app = await BuildAppWithEngineAsync(
            _storeReader, _storeWriter, _transitionHook,
            new TextRenderedContent("<p>Bonjour</p>", DocumentFormat.Html) { RevisionId = revisionId });
        using HttpClient client = BuildClient(app, AllPermissions);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"{Prefix}/Billing.Invoice/preview",
            new TemplatePreviewRequest("fr", null),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await _storeReader.Received(1).TryGetDraftAsync(
            Arg.Is<TemplateKey>(k => k.Name == "Billing.Invoice" && k.Culture == "fr"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Preview_WhenRenderingFails_Returns422()
    {
        _storeReader.TryGetDraftAsync(Arg.Any<TemplateKey>(), Arg.Any<CancellationToken>())
            .Returns(new TemplateRevision
            {
                RevisionId = Guid.NewGuid(),
                Content = "{{ invalid syntax",
                MimeType = "text/html",
                Status = WorkflowLifecycleStatus.Draft,
                Version = 1,
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedBy = "admin",
                ConcurrencyStamp = AnyStamp,
            });

        ITemplateEngine engine = Substitute.For<ITemplateEngine>();
        engine.CanRender(Arg.Any<TemplateDescriptor>()).Returns(true);
        engine.RenderAsync(
                Arg.Any<TemplateDescriptor>(),
                Arg.Any<Dictionary<string, object?>>(),
                Arg.Any<DocumentFormat>(),
                Arg.Any<IReadOnlyList<ITemplateGlobalContext>>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Parse error in template"));

        await using WebApplication app = await BuildAppWithEngineAsync(
            _storeReader, _storeWriter, _transitionHook, engine);
        using HttpClient client = BuildClient(app, AllPermissions);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"{Prefix}/Billing.Invoice/preview",
            new TemplatePreviewRequest(null, null),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Preview_WhenNoEngineCanRender_Returns422()
    {
        _storeReader.TryGetDraftAsync(Arg.Any<TemplateKey>(), Arg.Any<CancellationToken>())
            .Returns(new TemplateRevision
            {
                RevisionId = Guid.NewGuid(),
                Content = "binary-content",
                MimeType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                Status = WorkflowLifecycleStatus.Draft,
                Version = 1,
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedBy = "admin",
                ConcurrencyStamp = AnyStamp,
            });

        ITemplateEngine engine = Substitute.For<ITemplateEngine>();
        engine.CanRender(Arg.Any<TemplateDescriptor>()).Returns(false);

        await using WebApplication app = await BuildAppWithEngineAsync(
            _storeReader, _storeWriter, _transitionHook, engine);
        using HttpClient client = BuildClient(app, AllPermissions);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"{Prefix}/Billing.Invoice/preview",
            new TemplatePreviewRequest(null, null),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Preview_WithoutAuth_Returns401()
    {
        HttpResponseMessage response = await _anonClient.PostAsJsonAsync(
            $"{Prefix}/Billing.Invoice/preview",
            new TemplatePreviewRequest(null, null),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Preview_WithInvalidName_Returns400()
    {
        HttpResponseMessage response = await _adminClient.PostAsJsonAsync(
            $"{Prefix}/invalid/preview",
            new TemplatePreviewRequest(null, null),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Preview_WithEmptyData_UsesEmptyDictionary()
    {
        var revisionId = Guid.NewGuid();
        _storeReader.TryGetDraftAsync(Arg.Any<TemplateKey>(), Arg.Any<CancellationToken>())
            .Returns(new TemplateRevision
            {
                RevisionId = revisionId,
                Content = "<p>No data</p>",
                MimeType = "text/html",
                Status = WorkflowLifecycleStatus.Draft,
                Version = 1,
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedBy = "admin",
                ConcurrencyStamp = AnyStamp,
            });

        await using WebApplication app = await BuildAppWithEngineAsync(
            _storeReader, _storeWriter, _transitionHook,
            new TextRenderedContent("<p>No data</p>", DocumentFormat.Html) { RevisionId = revisionId });
        using HttpClient client = BuildClient(app, AllPermissions);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"{Prefix}/Billing.Invoice/preview",
            new TemplatePreviewRequest(null, null),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // =========================================================================
    // GET /{name}/variables — Template variables
    // =========================================================================

    [Fact]
    public async Task GetVariables_WithNoGlobalContexts_ReturnsEmptyLists()
    {
        HttpResponseMessage response = await _adminClient.GetAsync(
            $"{Prefix}/Billing.Invoice/variables",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        TemplateVariablesResponse? result =
            await response.Content.ReadFromJsonAsync<TemplateVariablesResponse>(
                TestContext.Current.CancellationToken);
        result.ShouldNotBeNull();
        result.GlobalVariables.ShouldBeEmpty();
        result.ModelVariables.ShouldBeEmpty();
        result.EnrichedVariables.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetVariables_WithGlobalContext_ReturnsDiscoveredVariables()
    {
        await using WebApplication app = await BuildAppWithGlobalContextAsync();
        using HttpClient client = BuildClient(app, AllPermissions);

        HttpResponseMessage response = await client.GetAsync(
            $"{Prefix}/Billing.Invoice/variables",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        TemplateVariablesResponse? result =
            await response.Content.ReadFromJsonAsync<TemplateVariablesResponse>(
                TestContext.Current.CancellationToken);
        result.ShouldNotBeNull();
        result.GlobalVariables.Count.ShouldBeGreaterThan(0);
        result.GlobalVariables.ShouldContain(v => v.Name == "test.first_name");
        result.GlobalVariables.ShouldContain(v => v.Name == "test.age");
        TemplateVariableItemResponse firstName = result.GlobalVariables.First(v => v.Name == "test.first_name");
        firstName.Type.ShouldBe("string");
        TemplateVariableItemResponse age = result.GlobalVariables.First(v => v.Name == "test.age");
        age.Type.ShouldBe("number");
    }

    [Fact]
    public async Task GetVariables_WithInvalidName_Returns400()
    {
        HttpResponseMessage response = await _adminClient.GetAsync(
            $"{Prefix}/bad/variables",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetVariables_WithoutAuth_Returns401()
    {
        HttpResponseMessage response = await _anonClient.GetAsync(
            $"{Prefix}/Billing.Invoice/variables",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private static async Task<WebApplication> BuildAppWithGlobalContextAsync()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services
            .AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthHandler.SchemeName, _ => { });

        builder.Services.AddAuthorizationBuilder()
            .AddPolicy(TemplatingPermissions.Templates.Read,
                policy => policy.RequireClaim(TestAuthHandler.PermissionClaimType, TemplatingPermissions.Templates.Read))
            .AddPolicy(TemplatingPermissions.Templates.Manage,
                policy => policy.RequireClaim(TestAuthHandler.PermissionClaimType, TemplatingPermissions.Templates.Manage))
            .AddPolicy(TemplatingPermissions.Categories.Read,
                policy => policy.RequireClaim(TestAuthHandler.PermissionClaimType, TemplatingPermissions.Categories.Read))
            .AddPolicy(TemplatingPermissions.Categories.Manage,
                policy => policy.RequireClaim(TestAuthHandler.PermissionClaimType, TemplatingPermissions.Categories.Manage));

        IDocumentTemplateStoreReader storeReader = Substitute.For<IDocumentTemplateStoreReader>();
        IDocumentTemplateStoreWriter storeWriter = Substitute.For<IDocumentTemplateStoreWriter>();
        builder.Services.AddSingleton(storeReader);
        builder.Services.AddSingleton(storeWriter);
        builder.Services.AddSingleton<IValidator<SaveTemplateRequest>, SaveTemplateRequestValidator>();
        builder.Services.AddSingleton<ITemplateGlobalContext, TestGlobalContext>();
        builder.Services.AddSingleton(CreateTestUserService());

        WebApplication app = builder.Build();
        app.MapGranitTemplating();
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    private static async Task<WebApplication> BuildAppWithEngineAsync(
        IDocumentTemplateStoreReader storeReader,
        IDocumentTemplateStoreWriter storeWriter,
        ITemplateTransitionHook transitionHook,
        RenderedContent renderedContent)
    {
        ITemplateEngine engine = Substitute.For<ITemplateEngine>();
        engine.CanRender(Arg.Any<TemplateDescriptor>()).Returns(true);
        engine.RenderAsync(
                Arg.Any<TemplateDescriptor>(),
                Arg.Any<Dictionary<string, object?>>(),
                Arg.Any<DocumentFormat>(),
                Arg.Any<IReadOnlyList<ITemplateGlobalContext>>(),
                Arg.Any<CancellationToken>())
            .Returns(renderedContent);

        return await BuildAppWithEngineAsync(storeReader, storeWriter, transitionHook, engine);
    }

    private static async Task<WebApplication> BuildAppWithEngineAsync(
        IDocumentTemplateStoreReader storeReader,
        IDocumentTemplateStoreWriter storeWriter,
        ITemplateTransitionHook transitionHook,
        ITemplateEngine engine)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services
            .AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthHandler.SchemeName, _ => { });

        builder.Services.AddAuthorizationBuilder()
            .AddPolicy(TemplatingPermissions.Templates.Read,
                policy => policy.RequireClaim(TestAuthHandler.PermissionClaimType, TemplatingPermissions.Templates.Read))
            .AddPolicy(TemplatingPermissions.Templates.Manage,
                policy => policy.RequireClaim(TestAuthHandler.PermissionClaimType, TemplatingPermissions.Templates.Manage))
            .AddPolicy(TemplatingPermissions.Categories.Read,
                policy => policy.RequireClaim(TestAuthHandler.PermissionClaimType, TemplatingPermissions.Categories.Read))
            .AddPolicy(TemplatingPermissions.Categories.Manage,
                policy => policy.RequireClaim(TestAuthHandler.PermissionClaimType, TemplatingPermissions.Categories.Manage));

        builder.Services.AddSingleton(storeReader);
        builder.Services.AddSingleton(storeWriter);
        builder.Services.AddSingleton(transitionHook);
        builder.Services.AddSingleton(engine);
        builder.Services.AddSingleton<IValidator<SaveTemplateRequest>, SaveTemplateRequestValidator>();
        builder.Services.AddSingleton(CreateTestUserService());

        WebApplication app = builder.Build();
        app.MapGranitTemplating();
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    private static async Task<WebApplication> BuildAppWithoutStoreAsync()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services
            .AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthHandler.SchemeName, _ => { });

        builder.Services.AddAuthorizationBuilder()
            .AddPolicy(TemplatingPermissions.Templates.Read,
                policy => policy.RequireClaim(TestAuthHandler.PermissionClaimType, TemplatingPermissions.Templates.Read))
            .AddPolicy(TemplatingPermissions.Templates.Manage,
                policy => policy.RequireClaim(TestAuthHandler.PermissionClaimType, TemplatingPermissions.Templates.Manage))
            .AddPolicy(TemplatingPermissions.Categories.Read,
                policy => policy.RequireClaim(TestAuthHandler.PermissionClaimType, TemplatingPermissions.Categories.Read))
            .AddPolicy(TemplatingPermissions.Categories.Manage,
                policy => policy.RequireClaim(TestAuthHandler.PermissionClaimType, TemplatingPermissions.Categories.Manage));

        WebApplication app = builder.Build();
        app.MapGranitTemplating();
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    private HttpClient BuildClient(params string[] permissions) => BuildClient(_app, permissions);

    private static HttpClient BuildClient(WebApplication app, params string[] permissions)
    {
        HttpClient client = app.GetTestClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.PermissionsHeader, string.Join(',', permissions));
        return client;
    }

    // =========================================================================
    // Fake global context for variable introspection tests
    // =========================================================================

    private static ICurrentUserService CreateTestUserService()
    {
        ICurrentUserService service = Substitute.For<ICurrentUserService>();
        service.UserId.Returns("test-user");
        service.UserName.Returns("test-user");
        return service;
    }

    private sealed class TestGlobalContext : ITemplateGlobalContext
    {
        public string ContextName => "test";

        public object Resolve() => new { FirstName = "John", Age = 42, Activated = true };
    }

    // =========================================================================
    // Fake authentication handler
    // =========================================================================

    private sealed class TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "Test";
        public const string PermissionsHeader = "X-Test-Permissions";
        public const string PermissionClaimType = "permission";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(PermissionsHeader, out Microsoft.Extensions.Primitives.StringValues permsHeader))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            string[] permissions = permsHeader.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries);
            Claim[] claims =
            [
                new(ClaimTypes.Name, "test-user"),
                .. permissions.Select(p => new Claim(PermissionClaimType, p.Trim())),
            ];

            ClaimsIdentity identity = new(claims, SchemeName);
            ClaimsPrincipal principal = new(identity);
            AuthenticationTicket ticket = new(principal, SchemeName);

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
