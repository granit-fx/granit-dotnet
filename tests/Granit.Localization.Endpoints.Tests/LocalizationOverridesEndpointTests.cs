using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Granit.Localization.Endpoints.Extensions;
using Granit.Localization.Endpoints.Permissions;
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
/// (GET/PUT/DELETE /localization/overrides). Uses a TestServer + NSubstitute
/// mocks for <see cref="ILocalizationOverrideStoreReader"/> and <see cref="ILocalizationOverrideStoreWriter"/>.
/// </summary>
public sealed class LocalizationOverridesEndpointTests : IAsyncDisposable
{
    private const string Prefix = "/localization/overrides";
    private const string ManageRole = "localization-admin";

    private readonly ILocalizationOverrideStoreReader _storeReader = Substitute.For<ILocalizationOverrideStoreReader>();
    private readonly ILocalizationOverrideStoreWriter _storeWriter = Substitute.For<ILocalizationOverrideStoreWriter>();
    private readonly WebApplication _app;
    private readonly HttpClient _adminClient;
    private readonly HttpClient _anonClient;

    public LocalizationOverridesEndpointTests()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services
            .AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthHandler.SchemeName, _ => { });

        // Register a role-based policy for the permission name so the TestAuthHandler can resolve it.
        builder.Services.AddAuthorizationBuilder()
            .AddPolicy(LocalizationOverridesPermissions.Overrides.Manage,
                policy => policy.RequireRole(ManageRole));

        builder.Services.AddSingleton(_storeReader);
        builder.Services.AddSingleton(_storeWriter);

        _app = builder.Build();
        _app.MapGranitLocalizationOverrides();
        _app.StartAsync().GetAwaiter().GetResult();

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
    // GET /localization/overrides
    // =========================================================================

    [Fact]
    public async Task GetOverrides_WhenStoreNotRegistered_Returns501()
    {
        // Arrange -- separate app without ILocalizationOverrideStore
        await using WebApplication app = await BuildAppWithoutStoreAsync();
        using HttpClient client = BuildClient(app, ManageRole);

        // Act
        HttpResponseMessage response = await client.GetAsync(
            $"{Prefix}?resourceName=Test&cultureName=fr",
            TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotImplemented);
    }

    [Fact]
    public async Task GetOverrides_WithoutResourceName_Returns400()
    {
        // Act
        HttpResponseMessage response = await _adminClient.GetAsync(
            $"{Prefix}?cultureName=fr",
            TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetOverrides_WithoutCultureName_Returns400()
    {
        // Act
        HttpResponseMessage response = await _adminClient.GetAsync(
            $"{Prefix}?resourceName=Test",
            TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetOverrides_WithInvalidBcp47CultureName_Returns400()
    {
        // Act
        HttpResponseMessage response = await _adminClient.GetAsync(
            $"{Prefix}?resourceName=Test&cultureName=en:invalid",
            TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetOverrides_WhenResourceNameExceeds200Chars_Returns400()
    {
        // Arrange
        string longName = new('x', 201);

        // Act
        HttpResponseMessage response = await _adminClient.GetAsync(
            $"{Prefix}?resourceName={longName}&cultureName=fr",
            TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetOverrides_WithValidParams_ReturnsEmptyDictionary()
    {
        // Arrange
        _storeReader.GetOverridesAsync("Test", "fr", Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, string>());

        // Act
        HttpResponseMessage response = await _adminClient.GetAsync(
            $"{Prefix}?resourceName=Test&cultureName=fr",
            TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        Dictionary<string, string>? result =
            await response.Content.ReadFromJsonAsync<Dictionary<string, string>>(
                TestContext.Current.CancellationToken);
        result.ShouldNotBeNull();
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetOverrides_WithPopulatedOverrides_Returns200WithEntries()
    {
        // Arrange
        Dictionary<string, string> overrides = new()
        {
            ["Hello"] = "Salut",
            ["Goodbye"] = "Ciao",
        };
        _storeReader.GetOverridesAsync("Test", "fr", Arg.Any<CancellationToken>())
            .Returns(overrides);

        // Act
        HttpResponseMessage response = await _adminClient.GetAsync(
            $"{Prefix}?resourceName=Test&cultureName=fr",
            TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        Dictionary<string, string>? result =
            await response.Content.ReadFromJsonAsync<Dictionary<string, string>>(
                TestContext.Current.CancellationToken);
        result.ShouldNotBeNull();
        result.Count.ShouldBe(2);
        result["Hello"].ShouldBe("Salut");
        result["Goodbye"].ShouldBe("Ciao");
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
            $"{Prefix}?resourceName=Test&cultureName=fr",
            TestContext.Current.CancellationToken);

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
            $"{Prefix}?resourceName=Test&cultureName=fr",
            TestContext.Current.CancellationToken);

        // Assert
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
        builder.Services
            .AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthHandler.SchemeName, _ => { });
        builder.Services.AddAuthorizationBuilder()
            .AddPolicy(LocalizationOverridesPermissions.Overrides.Manage,
                policy => policy.RequireRole(ManageRole));
        builder.Services.AddSingleton(_storeReader);
        builder.Services.AddSingleton(_storeWriter);

        _storeReader.GetOverridesAsync("Test", "fr", Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, string>());

        await using WebApplication app = builder.Build();
        app.MapGranitLocalizationOverrides(opts => opts.RoutePrefix = "i18n");
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = BuildClient(app, ManageRole);

        // Act -- default route must not be registered
        HttpResponseMessage notFound = await client.GetAsync(
            "/localization/overrides?resourceName=Test&cultureName=fr",
            TestContext.Current.CancellationToken);

        // Act -- custom route must respond
        HttpResponseMessage ok = await client.GetAsync(
            "/i18n/overrides?resourceName=Test&cultureName=fr",
            TestContext.Current.CancellationToken);

        // Assert
        notFound.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        ok.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    /// <summary>
    /// Builds a standalone WebApplication without registering <see cref="ILocalizationOverrideStoreReader"/>
    /// to test the 501 Not Implemented path.
    /// </summary>
    private static async Task<WebApplication> BuildAppWithoutStoreAsync()
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

        WebApplication app = builder.Build();
        app.MapGranitLocalizationOverrides();
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
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
