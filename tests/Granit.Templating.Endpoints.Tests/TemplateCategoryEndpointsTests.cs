using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using FluentValidation;
using Granit.Core.Exceptions;
using Granit.Templating.Endpoints.Dtos;
using Granit.Templating.Endpoints.Extensions;
using Granit.Templating.Endpoints.Permissions;
using Granit.Templating.Endpoints.Validators;
using Granit.Templating.Store;
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
/// Integration tests for the template category CRUD endpoints
/// (GET/POST/PUT/DELETE /categories). Uses a TestServer + NSubstitute mocks.
/// </summary>
public sealed class TemplateCategoryEndpointsTests : IAsyncDisposable
{
    private const string Prefix = "/templates/categories";
    private const string ManageRole = "template-admin";

    private readonly ITemplateCategoryStoreReader _categoryReader = Substitute.For<ITemplateCategoryStoreReader>();
    private readonly ITemplateCategoryStoreWriter _categoryWriter = Substitute.For<ITemplateCategoryStoreWriter>();
    private readonly WebApplication _app;
    private readonly HttpClient _adminClient;
    private readonly HttpClient _anonClient;

    public TemplateCategoryEndpointsTests()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services
            .AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthHandler.SchemeName, _ => { });

        builder.Services.AddAuthorizationBuilder()
            .AddPolicy(TemplatingPermissions.Templates.Read,
                policy => policy.RequireRole(ManageRole))
            .AddPolicy(TemplatingPermissions.Templates.Manage,
                policy => policy.RequireRole(ManageRole))
            .AddPolicy(TemplatingPermissions.Categories.Read,
                policy => policy.RequireRole(ManageRole))
            .AddPolicy(TemplatingPermissions.Categories.Manage,
                policy => policy.RequireRole(ManageRole));

        // Register category store mocks.
        builder.Services.AddSingleton(_categoryReader);
        builder.Services.AddSingleton(_categoryWriter);

        // Also register template store mocks (required by some endpoints in the same group).
        builder.Services.AddSingleton(Substitute.For<IDocumentTemplateStoreReader>());
        builder.Services.AddSingleton(Substitute.For<IDocumentTemplateStoreWriter>());
        builder.Services.AddSingleton(Substitute.For<ITemplateTransitionHook>());
        builder.Services.AddSingleton<IValidator<SaveTemplateRequest>, SaveTemplateRequestValidator>();
        builder.Services.AddSingleton<IValidator<SaveTemplateCategoryRequest>, SaveTemplateCategoryRequestValidator>();

        _app = builder.Build();
        _app.MapGranitTemplatingAdmin();
        _app.StartAsync().GetAwaiter().GetResult();

        _adminClient = BuildClient(_app, ManageRole);
        _anonClient = _app.GetTestClient();
    }

    public async ValueTask DisposeAsync()
    {
        _adminClient.Dispose();
        _anonClient.Dispose();
        await _app.DisposeAsync();
    }

    // =========================================================================
    // GET /categories — List categories
    // =========================================================================

    [Fact]
    public async Task ListCategories_WhenStoreNotRegistered_Returns501()
    {
        await using WebApplication app = await BuildAppWithoutCategoryStoreAsync();
        using HttpClient client = BuildClient(app, ManageRole);

        HttpResponseMessage response = await client.GetAsync(
            Prefix,
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotImplemented);
    }

    [Fact]
    public async Task ListCategories_WhenEmpty_ReturnsEmptyList()
    {
        _categoryReader.ListCategoriesAsync(Arg.Any<CancellationToken>())
            .Returns(Array.Empty<TemplateCategory>());

        HttpResponseMessage response = await _adminClient.GetAsync(
            Prefix,
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        List<TemplateCategoryResponse>? result =
            await response.Content.ReadFromJsonAsync<List<TemplateCategoryResponse>>(
                TestContext.Current.CancellationToken);
        result.ShouldNotBeNull();
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task ListCategories_WithResults_ReturnsMappedItems()
    {
        var category = new TemplateCategory
        {
            Id = Guid.NewGuid(),
            Name = "Patient Letters",
            Description = "Templates for patient correspondence",
            Icon = "file-text",
            SortOrder = 1,
            TemplateCount = 5,
        };

        _categoryReader.ListCategoriesAsync(Arg.Any<CancellationToken>())
            .Returns(new[] { category });

        HttpResponseMessage response = await _adminClient.GetAsync(
            Prefix,
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        List<TemplateCategoryResponse>? result =
            await response.Content.ReadFromJsonAsync<List<TemplateCategoryResponse>>(
                TestContext.Current.CancellationToken);
        result.ShouldNotBeNull();
        result.Count.ShouldBe(1);
        result[0].Id.ShouldBe(category.Id);
        result[0].Name.ShouldBe("Patient Letters");
        result[0].Description.ShouldBe("Templates for patient correspondence");
        result[0].Icon.ShouldBe("file-text");
        result[0].SortOrder.ShouldBe(1);
        result[0].TemplateCount.ShouldBe(5);
    }

    [Fact]
    public async Task ListCategories_WithoutAuth_Returns401()
    {
        HttpResponseMessage response = await _anonClient.GetAsync(
            Prefix,
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // =========================================================================
    // POST /categories — Create category
    // =========================================================================

    [Fact]
    public async Task CreateCategory_WithValidData_Returns201()
    {
        var expectedId = Guid.NewGuid();
        _categoryWriter.CreateCategoryAsync(
                "Invoices", "Invoice templates", "receipt", 0,
                Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new TemplateCategory
            {
                Id = expectedId,
                Name = "Invoices",
                Description = "Invoice templates",
                Icon = "receipt",
                SortOrder = 0,
                TemplateCount = 0,
            });

        HttpResponseMessage response = await _adminClient.PostAsJsonAsync(
            Prefix,
            new SaveTemplateCategoryRequest("Invoices", "Invoice templates", "receipt", 0),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        TemplateCategoryResponse? result =
            await response.Content.ReadFromJsonAsync<TemplateCategoryResponse>(
                TestContext.Current.CancellationToken);
        result.ShouldNotBeNull();
        result.Id.ShouldBe(expectedId);
        result.Name.ShouldBe("Invoices");
    }

    [Fact]
    public async Task CreateCategory_WithDuplicateName_Returns409()
    {
        _categoryWriter.CreateCategoryAsync(
                Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ConflictException(
                "A category with the name 'Invoices' already exists."));

        HttpResponseMessage response = await _adminClient.PostAsJsonAsync(
            Prefix,
            new SaveTemplateCategoryRequest("Invoices", null, null, 0),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CreateCategory_WithEmptyName_Returns422()
    {
        HttpResponseMessage response = await _adminClient.PostAsJsonAsync(
            Prefix,
            new SaveTemplateCategoryRequest("", null, null, 0),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task CreateCategory_WithoutAuth_Returns401()
    {
        HttpResponseMessage response = await _anonClient.PostAsJsonAsync(
            Prefix,
            new SaveTemplateCategoryRequest("Invoices", null, null, 0),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // =========================================================================
    // PUT /categories/{id} — Update category
    // =========================================================================

    [Fact]
    public async Task UpdateCategory_WithValidData_Returns200()
    {
        var categoryId = Guid.NewGuid();
        _categoryWriter.UpdateCategoryAsync(
                categoryId, "Updated Name", "New description", "edit",
                2, Arg.Any<CancellationToken>())
            .Returns(new TemplateCategory
            {
                Id = categoryId,
                Name = "Updated Name",
                Description = "New description",
                Icon = "edit",
                SortOrder = 2,
                TemplateCount = 3,
            });

        HttpResponseMessage response = await _adminClient.PutAsJsonAsync(
            $"{Prefix}/{categoryId}",
            new SaveTemplateCategoryRequest("Updated Name", "New description", "edit", 2),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        TemplateCategoryResponse? result =
            await response.Content.ReadFromJsonAsync<TemplateCategoryResponse>(
                TestContext.Current.CancellationToken);
        result.ShouldNotBeNull();
        result.Name.ShouldBe("Updated Name");
        result.SortOrder.ShouldBe(2);
    }

    [Fact]
    public async Task UpdateCategory_WhenNotFound_Returns404()
    {
        var categoryId = Guid.NewGuid();
        _categoryWriter.UpdateCategoryAsync(
                categoryId, Arg.Any<string>(), Arg.Any<string?>(),
                Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new EntityNotFoundException(typeof(object), categoryId));

        HttpResponseMessage response = await _adminClient.PutAsJsonAsync(
            $"{Prefix}/{categoryId}",
            new SaveTemplateCategoryRequest("Test", null, null, 0),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateCategory_WithDuplicateName_Returns409()
    {
        var categoryId = Guid.NewGuid();
        _categoryWriter.UpdateCategoryAsync(
                categoryId, Arg.Any<string>(), Arg.Any<string?>(),
                Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ConflictException(
                "A category with the name 'Invoices' already exists."));

        HttpResponseMessage response = await _adminClient.PutAsJsonAsync(
            $"{Prefix}/{categoryId}",
            new SaveTemplateCategoryRequest("Invoices", null, null, 0),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task UpdateCategory_WithEmptyName_Returns422()
    {
        var categoryId = Guid.NewGuid();

        HttpResponseMessage response = await _adminClient.PutAsJsonAsync(
            $"{Prefix}/{categoryId}",
            new SaveTemplateCategoryRequest("", null, null, 0),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    // =========================================================================
    // DELETE /categories/{id} — Delete category
    // =========================================================================

    [Fact]
    public async Task DeleteCategory_WhenEmpty_Returns204()
    {
        var categoryId = Guid.NewGuid();

        HttpResponseMessage response = await _adminClient.DeleteAsync(
            $"{Prefix}/{categoryId}",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await _categoryWriter.Received(1).DeleteCategoryAsync(categoryId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteCategory_WhenNotFound_Returns404()
    {
        var categoryId = Guid.NewGuid();
        _categoryWriter.DeleteCategoryAsync(categoryId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new EntityNotFoundException(typeof(object), categoryId));

        HttpResponseMessage response = await _adminClient.DeleteAsync(
            $"{Prefix}/{categoryId}",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteCategory_WhenTemplatesAssociated_Returns409()
    {
        var categoryId = Guid.NewGuid();
        _categoryWriter.DeleteCategoryAsync(categoryId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new ConflictException(
                "Cannot delete category with 3 associated template(s)."));

        HttpResponseMessage response = await _adminClient.DeleteAsync(
            $"{Prefix}/{categoryId}",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task DeleteCategory_WithoutAuth_Returns401()
    {
        var categoryId = Guid.NewGuid();

        HttpResponseMessage response = await _anonClient.DeleteAsync(
            $"{Prefix}/{categoryId}",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private static async Task<WebApplication> BuildAppWithoutCategoryStoreAsync()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services
            .AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthHandler.SchemeName, _ => { });

        builder.Services.AddAuthorizationBuilder()
            .AddPolicy(TemplatingPermissions.Templates.Read,
                policy => policy.RequireRole(ManageRole))
            .AddPolicy(TemplatingPermissions.Templates.Manage,
                policy => policy.RequireRole(ManageRole))
            .AddPolicy(TemplatingPermissions.Categories.Read,
                policy => policy.RequireRole(ManageRole))
            .AddPolicy(TemplatingPermissions.Categories.Manage,
                policy => policy.RequireRole(ManageRole));

        WebApplication app = builder.Build();
        app.MapGranitTemplatingAdmin();
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    private static HttpClient BuildClient(WebApplication app, string role)
    {
        HttpClient client = app.GetTestClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, role);
        return client;
    }

    private sealed class TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "Test";
        public const string RolesHeader = "X-Test-Roles";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(RolesHeader, out Microsoft.Extensions.Primitives.StringValues rolesHeader))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            string[] roles = rolesHeader.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries);
            Claim[] claims =
            [
                new(ClaimTypes.Name, "test-user"),
                .. roles.Select(r => new Claim(ClaimTypes.Role, r.Trim())),
            ];

            ClaimsIdentity identity = new(claims, SchemeName);
            ClaimsPrincipal principal = new(identity);
            AuthenticationTicket ticket = new(principal, SchemeName);

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
