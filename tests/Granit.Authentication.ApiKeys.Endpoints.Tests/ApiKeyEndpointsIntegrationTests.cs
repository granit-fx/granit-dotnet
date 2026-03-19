using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Granit.Authentication.ApiKeys.Domain;
using Granit.Authentication.ApiKeys.Endpoints.Dtos;
using Granit.Authentication.ApiKeys.Endpoints.Extensions;
using Granit.Authentication.ApiKeys.Endpoints.Permissions;
using Granit.Guids;
using Granit.Querying;
using Granit.Timing;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Authentication.ApiKeys.Endpoints.Tests;

/// <summary>
/// Integration tests for API key management endpoints using TestServer + NSubstitute mocks.
/// </summary>
public sealed class ApiKeyEndpointsIntegrationTests : IAsyncDisposable
{
    private const string Prefix = "/api-keys";
    private const string AdminRole = "granit-apikeys-admin";

    private readonly IApiKeyAdminStore _adminStore = Substitute.For<IApiKeyAdminStore>();
    private readonly IApiKeyGenerator _generator = Substitute.For<IApiKeyGenerator>();
    private readonly IGuidGenerator _guidGenerator = Substitute.For<IGuidGenerator>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly WebApplication _app;
    private readonly HttpClient _adminClient;
    private readonly HttpClient _anonClient;

    public ApiKeyEndpointsIntegrationTests()
    {
        DateTimeOffset now = new(2026, 3, 9, 12, 0, 0, TimeSpan.Zero);
        _clock.Now.Returns(now);

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services
            .AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthHandler.SchemeName, _ => { });

        builder.Services.AddAuthorizationBuilder()
            .AddPolicy(ApiKeyPermissions.Keys.Read, policy => policy.RequireRole(AdminRole))
            .AddPolicy(ApiKeyPermissions.Keys.Create, policy => policy.RequireRole(AdminRole))
            .AddPolicy(ApiKeyPermissions.Keys.Revoke, policy => policy.RequireRole(AdminRole))
            .AddPolicy(ApiKeyPermissions.Keys.Rotate, policy => policy.RequireRole(AdminRole))
            .AddPolicy(ApiKeyPermissions.Keys.UpdateScopes, policy => policy.RequireRole(AdminRole));

        builder.Services.AddSingleton(_adminStore);
        builder.Services.AddSingleton(_generator);
        builder.Services.AddSingleton(_guidGenerator);
        builder.Services.AddSingleton(_clock);

        _app = builder.Build();
        _app.MapApiKeysEndpoints();
        _app.StartAsync().GetAwaiter().GetResult();

        _adminClient = BuildClient(_app, AdminRole);
        _anonClient = _app.GetTestClient();
    }

    public async ValueTask DisposeAsync()
    {
        _adminClient.Dispose();
        _anonClient.Dispose();
        await _app.DisposeAsync();
    }

    // =========================================================================
    // GET /api-keys — List API keys
    // =========================================================================

    [Fact]
    public async Task ListApiKeys_ReturnsOkWithPagedResult()
    {
        ApiKeyEntry entry = CreateSampleEntry();
        _adminStore.ListAsync(
                null, null, null, false, 1, 20,
                Arg.Any<CancellationToken>())
            .Returns(new PagedResult<ApiKeyEntry>([entry], 1, HasMore: false));

        HttpResponseMessage response = await _adminClient.GetAsync(
            Prefix,
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        PagedResult<ApiKeyResponse>? result =
            await response.Content.ReadFromJsonAsync<PagedResult<ApiKeyResponse>>(
                TestContext.Current.CancellationToken);
        result.ShouldNotBeNull();
        result.TotalCount.ShouldBe(1);
        result.Items.Count.ShouldBe(1);
        result.Items[0].Name.ShouldBe("Test Key");
    }

    [Fact]
    public async Task ListApiKeys_WithoutAuth_Returns401()
    {
        HttpResponseMessage response = await _anonClient.GetAsync(
            Prefix,
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ListApiKeys_WithFilters_PassesParametersToStore()
    {
        _adminStore.ListAsync(
                "search", ApiKeyType.Secret, "live", true, 2, 10,
                Arg.Any<CancellationToken>())
            .Returns(new PagedResult<ApiKeyEntry>([], 0, HasMore: false));

        HttpResponseMessage response = await _adminClient.GetAsync(
            $"{Prefix}?search=search&type=Secret&environment=live&includeRevoked=true&page=2&pageSize=10",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await _adminStore.Received(1).ListAsync(
            "search", ApiKeyType.Secret, "live", true, 2, 10,
            Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // GET /api-keys/{id} — Get API key by ID
    // =========================================================================

    [Fact]
    public async Task GetApiKeyById_WhenExists_ReturnsOk()
    {
        ApiKeyEntry entry = CreateSampleEntry();
        _adminStore.FindByIdAsync(entry.Id, Arg.Any<CancellationToken>())
            .Returns(entry);

        HttpResponseMessage response = await _adminClient.GetAsync(
            $"{Prefix}/{entry.Id}",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        ApiKeyResponse? result =
            await response.Content.ReadFromJsonAsync<ApiKeyResponse>(
                TestContext.Current.CancellationToken);
        result.ShouldNotBeNull();
        result.Id.ShouldBe(entry.Id);
        result.Name.ShouldBe(entry.Name);
        result.Prefix.ShouldBe(entry.Prefix);
    }

    [Fact]
    public async Task GetApiKeyById_WhenNotFound_Returns404()
    {
        var id = Guid.NewGuid();
        _adminStore.FindByIdAsync(id, Arg.Any<CancellationToken>())
            .Returns((ApiKeyEntry?)null);

        HttpResponseMessage response = await _adminClient.GetAsync(
            $"{Prefix}/{id}",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetApiKeyById_WithoutAuth_Returns401()
    {
        var id = Guid.NewGuid();

        HttpResponseMessage response = await _anonClient.GetAsync(
            $"{Prefix}/{id}",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // =========================================================================
    // POST /api-keys — Create API key
    // =========================================================================

    [Fact]
    public async Task CreateApiKey_WithValidData_Returns201WithSecret()
    {
        var newId = Guid.NewGuid();
        _guidGenerator.Create().Returns(newId);
        _generator.Generate(ApiKeyType.Secret, "live")
            .Returns(new ApiKeyGenerationResult(
                "gk_live_sk_abc123xyz", "hash123", "gk_live_sk_", "3xyz"));

        HttpResponseMessage response = await _adminClient.PostAsJsonAsync(
            Prefix,
            new ApiKeyCreateRequest("My Key", ApiKeyType.Secret, "live"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        ApiKeyCreateResponse? result =
            await response.Content.ReadFromJsonAsync<ApiKeyCreateResponse>(
                TestContext.Current.CancellationToken);
        result.ShouldNotBeNull();
        result.Id.ShouldBe(newId);
        result.RawSecret.ShouldBe("gk_live_sk_abc123xyz");
        result.Prefix.ShouldBe("gk_live_sk_");
        result.Name.ShouldBe("My Key");

        await _adminStore.Received(1).CreateAsync(
            Arg.Is<ApiKeyEntry>(e => e.Id == newId && e.HashedKey == "hash123"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateApiKey_WithPermissionsAndCidrs_StoresThem()
    {
        var newId = Guid.NewGuid();
        _guidGenerator.Create().Returns(newId);
        _generator.Generate(ApiKeyType.Secret, "live")
            .Returns(new ApiKeyGenerationResult("gk_live_sk_x", "h", "gk_live_sk_", "xx_x"));

        var request = new ApiKeyCreateRequest(
            "Scoped Key", ApiKeyType.Secret, "live",
            ["Patients.Read", "Patients.Write"],
            ["10.0.0.0/24"],
            DateTimeOffset.UtcNow.AddDays(30),
            CacheBehavior.NoCache);

        HttpResponseMessage response = await _adminClient.PostAsJsonAsync(
            Prefix, request,
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        await _adminStore.Received(1).CreateAsync(
            Arg.Is<ApiKeyEntry>(e =>
                e.Permissions.Count == 2 &&
                e.AllowedCidrs.Count == 1 &&
                e.CacheBehavior == CacheBehavior.NoCache),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateApiKey_WithoutAuth_Returns401()
    {
        HttpResponseMessage response = await _anonClient.PostAsJsonAsync(
            Prefix,
            new ApiKeyCreateRequest("Key", ApiKeyType.Secret, "live"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // =========================================================================
    // POST /api-keys/{id}/revoke — Revoke API key
    // =========================================================================

    [Fact]
    public async Task RevokeApiKey_WhenExists_Returns204()
    {
        var id = Guid.NewGuid();
        _adminStore.RevokeAsync(id, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(true);

        HttpResponseMessage response = await _adminClient.PostAsync(
            $"{Prefix}/{id}/revoke", null,
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await _adminStore.Received(1).RevokeAsync(id, _clock.Now, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RevokeApiKey_WhenNotFound_Returns404()
    {
        var id = Guid.NewGuid();
        _adminStore.RevokeAsync(id, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(false);

        HttpResponseMessage response = await _adminClient.PostAsync(
            $"{Prefix}/{id}/revoke", null,
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RevokeApiKey_WithoutAuth_Returns401()
    {
        var id = Guid.NewGuid();

        HttpResponseMessage response = await _anonClient.PostAsync(
            $"{Prefix}/{id}/revoke", null,
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // =========================================================================
    // POST /api-keys/{id}/rotate — Rotate API key
    // =========================================================================

    [Fact]
    public async Task RotateApiKey_WhenExists_ReturnsOkWithNewSecret()
    {
        var oldId = Guid.NewGuid();
        var newId = Guid.NewGuid();
        ApiKeyEntry existing = CreateSampleEntry(oldId);

        _adminStore.FindByIdAsync(oldId, Arg.Any<CancellationToken>())
            .Returns(existing);
        _adminStore.RevokeAsync(oldId, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _guidGenerator.Create().Returns(newId);
        _generator.Generate(existing.Type, existing.Environment)
            .Returns(new ApiKeyGenerationResult(
                "gk_live_sk_newkey123", "newhash", "gk_live_sk_", "y123"));

        HttpResponseMessage response = await _adminClient.PostAsync(
            $"{Prefix}/{oldId}/rotate", null,
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        ApiKeyRotateResponse? result =
            await response.Content.ReadFromJsonAsync<ApiKeyRotateResponse>(
                TestContext.Current.CancellationToken);
        result.ShouldNotBeNull();
        result.NewKeyId.ShouldBe(newId);
        result.RawSecret.ShouldBe("gk_live_sk_newkey123");
        result.OldKeyId.ShouldBe(oldId);
    }

    [Fact]
    public async Task RotateApiKey_WhenNotFound_Returns404()
    {
        var id = Guid.NewGuid();
        _adminStore.FindByIdAsync(id, Arg.Any<CancellationToken>())
            .Returns((ApiKeyEntry?)null);

        HttpResponseMessage response = await _adminClient.PostAsync(
            $"{Prefix}/{id}/rotate", null,
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RotateApiKey_WhenAlreadyRevoked_Returns404()
    {
        var id = Guid.NewGuid();
        ApiKeyEntry revoked = CreateSampleEntry(id);
        revoked.Revoke(DateTimeOffset.UtcNow.AddDays(-1));

        _adminStore.FindByIdAsync(id, Arg.Any<CancellationToken>())
            .Returns(revoked);

        HttpResponseMessage response = await _adminClient.PostAsync(
            $"{Prefix}/{id}/rotate", null,
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RotateApiKey_PreservesPermissionsAndCidrs()
    {
        var oldId = Guid.NewGuid();
        var newId = Guid.NewGuid();
        ApiKeyEntry existing = CreateSampleEntry(oldId);
        existing.UpdatePermissions(["Patients.Read", "Patients.Write"]);
        existing.UpdateAllowedCidrs(["10.0.0.0/24"]);
        existing.SetCacheBehavior(CacheBehavior.NoCache);

        _adminStore.FindByIdAsync(oldId, Arg.Any<CancellationToken>())
            .Returns(existing);
        _adminStore.RevokeAsync(oldId, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _guidGenerator.Create().Returns(newId);
        _generator.Generate(existing.Type, existing.Environment)
            .Returns(new ApiKeyGenerationResult("gk_live_sk_new", "h", "gk_live_sk_", "new_"));

        await _adminClient.PostAsync(
            $"{Prefix}/{oldId}/rotate", null,
            TestContext.Current.CancellationToken);

        await _adminStore.Received(1).CreateAsync(
            Arg.Is<ApiKeyEntry>(e =>
                e.Id == newId &&
                e.Permissions.Count == 2 &&
                e.AllowedCidrs.Count == 1 &&
                e.CacheBehavior == CacheBehavior.NoCache),
            Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // PUT /api-keys/{id}/scopes — Update scopes
    // =========================================================================

    [Fact]
    public async Task UpdateScopes_WhenExists_Returns204()
    {
        var id = Guid.NewGuid();
        _adminStore.UpdateScopesAsync(
                id, Arg.Any<List<string>>(), Arg.Any<List<string>>(),
                Arg.Any<CancellationToken>())
            .Returns(true);

        HttpResponseMessage response = await _adminClient.PutAsJsonAsync(
            $"{Prefix}/{id}/scopes",
            new ApiKeyUpdateScopesRequest(["Read"], ["10.0.0.0/8"]),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task UpdateScopes_WhenNotFound_Returns404()
    {
        var id = Guid.NewGuid();
        _adminStore.UpdateScopesAsync(
                id, Arg.Any<List<string>>(), Arg.Any<List<string>>(),
                Arg.Any<CancellationToken>())
            .Returns(false);

        HttpResponseMessage response = await _adminClient.PutAsJsonAsync(
            $"{Prefix}/{id}/scopes",
            new ApiKeyUpdateScopesRequest([], []),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateScopes_WithoutAuth_Returns401()
    {
        var id = Guid.NewGuid();

        HttpResponseMessage response = await _anonClient.PutAsJsonAsync(
            $"{Prefix}/{id}/scopes",
            new ApiKeyUpdateScopesRequest([], []),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private static ApiKeyEntry CreateSampleEntry(Guid? id = null)
    {
        var entry = ApiKeyEntry.Create(
            id ?? Guid.NewGuid(),
            "Test Key",
            ApiKeyType.Secret,
            "live",
            "fake-sha256-hash-for-test-only", // gitleaks:allow
            "gk_live_sk_",
            "Ab1x");
        entry.CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        return entry;
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
