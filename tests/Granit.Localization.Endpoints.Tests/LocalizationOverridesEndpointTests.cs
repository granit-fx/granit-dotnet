using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Granit.Localization.Domain;
using Granit.Localization.Endpoints.Extensions;
using Granit.Localization.Endpoints.Permissions;
using Granit.Localization.Queries;
using Granit.MultiTenancy;
using Granit.QueryEngine;
using Granit.QueryEngine.Meta;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Localization.Endpoints.Tests;

/// <summary>
/// Integration tests for the localization override management endpoints
/// (GET / + /meta query-engine surface, plus PUT/DELETE CRUD).
/// Uses a TestServer + NSubstitute mocks for the query engine and override stores.
/// </summary>
public sealed class LocalizationOverridesEndpointTests : IAsyncDisposable
{
    private const string Prefix = "/localization/overrides";
    private const string ManageRole = "localization-admin";

    private readonly ILocalizationOverrideStoreReader _storeReader = Substitute.For<ILocalizationOverrideStoreReader>();
    private readonly ILocalizationOverrideStoreWriter _storeWriter = Substitute.For<ILocalizationOverrideStoreWriter>();
    private readonly IQueryEngine<LocalizationOverride> _engine = Substitute.For<IQueryEngine<LocalizationOverride>>();
    private readonly IQueryableSource<LocalizationOverride> _queryableSource = Substitute.For<IQueryableSource<LocalizationOverride>>();
    private readonly WebApplication _app;
    private readonly HttpClient _adminClient;
    private readonly HttpClient _anonClient;

    public LocalizationOverridesEndpointTests()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        ConfigureCommonServices(builder.Services);

        _app = builder.Build();
        _app.MapGranitLocalizationOverrides();
        _app.StartAsync().GetAwaiter().GetResult();

        _queryableSource.GetQueryable().Returns(Array.Empty<LocalizationOverride>().AsQueryable());

        _adminClient = BuildClient(ManageRole);
        _anonClient = _app.GetTestClient();
    }

    public async ValueTask DisposeAsync()
    {
        _adminClient.Dispose();
        _anonClient.Dispose();
        await _app.DisposeAsync();
    }

    // =========================================================================
    // GET /localization/overrides — query engine list
    // =========================================================================

    [Fact]
    public async Task GetOverrides_WithNoFilters_Returns200WithPagedResult()
    {
        // Arrange
        PagedResult<LocalizationOverride> expected = new(
            [new LocalizationOverride { ResourceName = "Acme", CultureName = "fr", Key = "hello", Value = "Salut" }],
            1, HasMore: false);

        _engine.ExecuteAsync(
            Arg.Any<IQueryable<LocalizationOverride>>(),
            Arg.Any<QueryRequest>(),
            Arg.Any<CancellationToken>())
            .Returns(expected);

        // Act
        HttpResponseMessage response = await _adminClient.GetAsync(
            Prefix, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        PagedResult<LocalizationOverride>? result = await response.Content
            .ReadFromJsonAsync<PagedResult<LocalizationOverride>>(TestContext.Current.CancellationToken);
        result.ShouldNotBeNull();
        result.Items.Count.ShouldBe(1);
        result.Items[0].Key.ShouldBe("hello");
    }

    [Fact]
    public async Task GetOverrides_PropagatesPaginationAndSort()
    {
        // Arrange
        _engine.ExecuteAsync(
            Arg.Any<IQueryable<LocalizationOverride>>(),
            Arg.Any<QueryRequest>(),
            Arg.Any<CancellationToken>())
            .Returns(new PagedResult<LocalizationOverride>([], 0, HasMore: false));

        // Act
        await _adminClient.GetAsync(
            $"{Prefix}?page=2&pageSize=20&sort=key",
            TestContext.Current.CancellationToken);

        // Assert
        await _engine.Received(1).ExecuteAsync(
            Arg.Any<IQueryable<LocalizationOverride>>(),
            Arg.Is<QueryRequest>(r => r.Page == 2 && r.PageSize == 20 && r.Sort == "key"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetOverrides_UsesIQueryableSourceFromDI()
    {
        // Arrange
        _engine.ExecuteAsync(
            Arg.Any<IQueryable<LocalizationOverride>>(),
            Arg.Any<QueryRequest>(),
            Arg.Any<CancellationToken>())
            .Returns(new PagedResult<LocalizationOverride>([], 0, HasMore: false));

        // Act
        await _adminClient.GetAsync(Prefix, TestContext.Current.CancellationToken);

        // Assert -- proves MapGranitQuery resolves IQueryableSource<LocalizationOverride>
        // from DI; the source is what bridges the showcase frontend to the EF Core layer.
        _queryableSource.Received().GetQueryable();
    }

    // =========================================================================
    // GET /localization/overrides/meta
    // =========================================================================

    [Fact]
    public async Task GetOverridesMeta_Returns200WithQueryMetadata()
    {
        // Arrange
        QueryMetadata expected = new()
        {
            Columns = [new ColumnDefinition("key", "Key", "String", 0, true, true, true, null)],
            FilterableFields = [],
            SortableFields = [],
            PresetFilterGroups = [],
            QuickFilters = [],
            DateFilters = [],
            GroupByFields = [],
            Pagination = new PaginationMeta(25, 100, QueryEngineDefaults.MaxStreamSize, false),
            DefaultSort = "-modifiedAt",
        };
        _engine.GetMetadata().Returns(expected);

        // Act
        HttpResponseMessage response = await _adminClient.GetAsync(
            $"{Prefix}/meta", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        QueryMetadata? result = await response.Content
            .ReadFromJsonAsync<QueryMetadata>(TestContext.Current.CancellationToken);
        result.ShouldNotBeNull();
        result.Columns.Count.ShouldBe(1);
        result.DefaultSort.ShouldBe("-modifiedAt");
    }

    // =========================================================================
    // PUT /localization/overrides/{resourceName}/{cultureName}/{key}
    // =========================================================================

    [Fact]
    public async Task PutOverride_WhenStoreNotRegistered_Returns501()
    {
        // Arrange
        await using WebApplication app = await BuildAppWithoutStoreAsync();
        using HttpClient client = BuildClient(app, ManageRole);

        // Act
        HttpResponseMessage response = await client.PutAsJsonAsync(
            $"{Prefix}/Test/fr/Hello",
            new { Value = "Salut" },
            TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotImplemented);
    }

    [Fact]
    public async Task PutOverride_WithInvalidBcp47CultureName_Returns400()
    {
        // Act
        HttpResponseMessage response = await _adminClient.PutAsJsonAsync(
            $"{Prefix}/Test/en:invalid/Hello",
            new { Value = "Hi" },
            TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PutOverride_WhenResourceNameExceeds200Chars_Returns400()
    {
        // Arrange
        string longName = new('x', 201);

        // Act
        HttpResponseMessage response = await _adminClient.PutAsJsonAsync(
            $"{Prefix}/{longName}/fr/Hello",
            new { Value = "Hi" },
            TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PutOverride_WhenKeyExceeds500Chars_Returns400()
    {
        // Arrange
        string longKey = new('k', 501);

        // Act
        HttpResponseMessage response = await _adminClient.PutAsJsonAsync(
            $"{Prefix}/Test/fr/{longKey}",
            new { Value = "Hi" },
            TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PutOverride_WithEmptyValue_Returns400()
    {
        // Act
        HttpResponseMessage response = await _adminClient.PutAsJsonAsync(
            $"{Prefix}/Test/fr/Hello",
            new { Value = "" },
            TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PutOverride_WhenValueExceeds4000Chars_Returns400()
    {
        // Arrange
        string longValue = new('v', 4001);

        // Act
        HttpResponseMessage response = await _adminClient.PutAsJsonAsync(
            $"{Prefix}/Test/fr/Hello",
            new { Value = longValue },
            TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PutOverride_WithValidRequest_Returns204()
    {
        // Arrange
        _storeWriter.SetOverrideAsync("Test", "fr", "Hello", "Salut", Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Act
        HttpResponseMessage response = await _adminClient.PutAsJsonAsync(
            $"{Prefix}/Test/fr/Hello",
            new { Value = "Salut" },
            TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await _storeWriter.Received(1).SetOverrideAsync("Test", "fr", "Hello", "Salut", Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // DELETE /localization/overrides/{resourceName}/{cultureName}/{key}
    // =========================================================================

    [Fact]
    public async Task DeleteOverride_WhenStoreNotRegistered_Returns501()
    {
        // Arrange
        await using WebApplication app = await BuildAppWithoutStoreAsync();
        using HttpClient client = BuildClient(app, ManageRole);

        // Act
        HttpResponseMessage response = await client.DeleteAsync(
            $"{Prefix}/Test/fr/Hello",
            TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotImplemented);
    }

    [Fact]
    public async Task DeleteOverride_WithInvalidBcp47CultureName_Returns400()
    {
        // Act
        HttpResponseMessage response = await _adminClient.DeleteAsync(
            $"{Prefix}/Test/en:invalid/Hello",
            TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DeleteOverride_WithValidRequest_Returns204()
    {
        // Arrange
        _storeWriter.RemoveOverrideAsync("Test", "fr", "Hello", Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Act
        HttpResponseMessage response = await _adminClient.DeleteAsync(
            $"{Prefix}/Test/fr/Hello",
            TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await _storeWriter.Received(1).RemoveOverrideAsync("Test", "fr", "Hello", Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // Security tests
    // =========================================================================

    [Fact]
    public async Task GetOverrides_WithoutToken_Returns401()
    {
        // Act
        HttpResponseMessage response = await _anonClient.GetAsync(
            Prefix, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetOverridesMeta_WithoutToken_Returns401()
    {
        // Act
        HttpResponseMessage response = await _anonClient.GetAsync(
            $"{Prefix}/meta", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PutOverride_WithoutToken_Returns401()
    {
        // Act
        HttpResponseMessage response = await _anonClient.PutAsJsonAsync(
            $"{Prefix}/Test/fr/Hello",
            new { Value = "Salut" },
            TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteOverride_WithoutToken_Returns401()
    {
        // Act
        HttpResponseMessage response = await _anonClient.DeleteAsync(
            $"{Prefix}/Test/fr/Hello",
            TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetOverrides_WithWrongRole_Returns403()
    {
        // Arrange
        using HttpClient client = BuildClient(_app, "regular-user");

        // Act
        HttpResponseMessage response = await client.GetAsync(
            Prefix, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetOverridesMeta_WithWrongRole_Returns403()
    {
        // Arrange
        using HttpClient client = BuildClient(_app, "regular-user");

        // Act
        HttpResponseMessage response = await client.GetAsync(
            $"{Prefix}/meta", TestContext.Current.CancellationToken);

        // Assert -- ensures the query-engine surface inherits the group's Manage policy.
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // =========================================================================
    // Route prefix tests
    // =========================================================================

    [Fact]
    public async Task MapGranitLocalizationOverrides_WithCustomRoutePrefix_RespondsOnCustomRoute()
    {
        // Arrange
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        ConfigureCommonServices(builder.Services);

        IQueryEngine<LocalizationOverride> engine = Substitute.For<IQueryEngine<LocalizationOverride>>();
        engine.ExecuteAsync(
            Arg.Any<IQueryable<LocalizationOverride>>(),
            Arg.Any<QueryRequest>(),
            Arg.Any<CancellationToken>())
            .Returns(new PagedResult<LocalizationOverride>([], 0, HasMore: false));
        builder.Services.AddSingleton(engine);

        await using WebApplication app = builder.Build();
        app.MapGranitLocalizationOverrides(opts => opts.RoutePrefix = "i18n");
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = BuildClient(app, ManageRole);

        // Act -- default route must not be registered
        HttpResponseMessage notFound = await client.GetAsync(
            "/localization/overrides", TestContext.Current.CancellationToken);

        // Act -- custom route must respond
        HttpResponseMessage ok = await client.GetAsync(
            "/i18n/overrides", TestContext.Current.CancellationToken);

        // Assert
        notFound.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        ok.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    /// <summary>
    /// Builds a standalone WebApplication without registering <see cref="ILocalizationOverrideStoreWriter"/>
    /// to test the 501 Not Implemented path on PUT/DELETE.
    /// </summary>
    private async Task<WebApplication> BuildAppWithoutStoreAsync()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services
            .AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthHandler.SchemeName, _ => { });

        builder.Services.AddAuthorizationBuilder()
            .AddPolicy(LocalizationOverridesPermissions.Overrides.Manage,
                policy => policy.RequireRole(ManageRole));

        // Query-engine surface still needs an engine + source so MapGranitQuery resolves cleanly;
        // only the writer is intentionally absent here.
        builder.Services.AddSingleton(_engine);
        builder.Services.AddSingleton(_queryableSource);
        builder.Services.AddSingleton<QueryDefinition<LocalizationOverride>, LocalizationOverrideQueryDefinition>();
        builder.Services.AddSingleton<ICurrentTenant>(Substitute.For<ICurrentTenant>());

        WebApplication app = builder.Build();
        app.MapGranitLocalizationOverrides();
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    private void ConfigureCommonServices(IServiceCollection services)
    {
        services
            .AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthHandler.SchemeName, _ => { });

        services.AddAuthorizationBuilder()
            .AddPolicy(LocalizationOverridesPermissions.Overrides.Manage,
                policy => policy.RequireRole(ManageRole));

        services.AddSingleton(_storeReader);
        services.AddSingleton(_storeWriter);
        services.AddSingleton(_engine);
        services.AddSingleton(_queryableSource);
        services.AddSingleton<QueryDefinition<LocalizationOverride>, LocalizationOverrideQueryDefinition>();
        services.AddSingleton<ICurrentTenant>(Substitute.For<ICurrentTenant>());
    }

    private HttpClient BuildClient(string role) => BuildClient(_app, role);

    private static HttpClient BuildClient(WebApplication app, string role)
    {
        HttpClient client = app.GetTestClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, role);
        return client;
    }

    // =========================================================================
    // Fake authentication handler (same pattern as BackgroundJobsEndpointsTests)
    // =========================================================================

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
