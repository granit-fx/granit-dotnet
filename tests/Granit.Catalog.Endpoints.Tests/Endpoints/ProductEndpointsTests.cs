using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Granit.Catalog;
using Granit.Catalog.Domain;
using Granit.Catalog.Endpoints.Dtos;
using Granit.Catalog.Endpoints.Extensions;
using Granit.Catalog.Endpoints.Permissions;
using Granit.Guids;
using Granit.MultiTenancy;
using Granit.QueryEngine;
using Granit.QueryEngine.Extensions;
using Granit.Workflow.Domain;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Catalog.Endpoints.Tests.Endpoints;

/// <summary>
/// Integration tests for the catalog product endpoints.
/// Uses a TestServer + NSubstitute mocks for IProductReader / IProductWriter.
/// A custom TestAuthHandler resolves authentication from X-Test-Roles header.
/// </summary>
public sealed class ProductEndpointsTests : IAsyncDisposable
{
    private const string AdminRole = "granit-catalog-admin";
    private const string Prefix = "/catalog/products";
    private static readonly Guid TestProductId = Guid.Parse("00000000-0000-0000-0000-000000000aaa");
    private static readonly Guid GeneratedId = Guid.Parse("00000000-0000-0000-0000-000000000bbb");

    private readonly IProductReader _reader = Substitute.For<IProductReader>();
    private readonly IProductWriter _writer = Substitute.For<IProductWriter>();
    private readonly IGuidGenerator _guidGenerator = Substitute.For<IGuidGenerator>();
    private readonly WebApplication _app;

    private readonly HttpClient _adminClient;
    private readonly HttpClient _userClient;
    private readonly HttpClient _anonClient;

    public ProductEndpointsTests()
    {
        _guidGenerator.Create().Returns(GeneratedId);

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services
            .AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthHandler.SchemeName, _ => { });

        builder.Services.AddAuthorizationBuilder()
            .AddPolicy(CatalogPermissions.Products.Read, p => p.RequireRole(AdminRole))
            .AddPolicy(CatalogPermissions.Products.Manage, p => p.RequireRole(AdminRole));

        builder.Services.AddSingleton(_reader);
        builder.Services.AddSingleton(_writer);
        builder.Services.AddSingleton(_guidGenerator);

        // Required by MapGranitCatalog's QueryEngine surface (/catalog/product-records).
        // We register the minimum services needed for route registration to succeed;
        // the QueryEngine routes themselves are exercised by integration tests in the
        // EntityFrameworkCore.Tests suite once an IQueryableSource<Product> backed by
        // the real DbContext exists.
        builder.Services.AddGranitQueryEngine();
        builder.Services.AddQueryDefinition<Product, Catalog.Queries.ProductQueryDefinition>();
        builder.Services.AddSingleton(Substitute.For<IQueryableSource<Product>>());
        builder.Services.AddSingleton(Substitute.For<ICurrentTenant>());

        _app = builder.Build();
        _app.MapGranitCatalog();
        _app.StartAsync().GetAwaiter().GetResult();

        _adminClient = BuildClient(AdminRole);
        _userClient = BuildClient("regular-user");
        _anonClient = _app.GetTestClient();
    }

    public async ValueTask DisposeAsync() => await _app.DisposeAsync();

    // ── List published products ───────────────────────────────────────────────

    [Fact]
    public async Task ListPublishedProducts_WithAdminToken_Returns200()
    {
        var p = Product.Create(TestProductId, "API", "API Calls", ProductType.Metered, "call");
        p.Publish();
        _reader.GetByStatusAsync(WorkflowLifecycleStatus.Published, Arg.Any<CancellationToken>())
               .Returns([p]);

        HttpResponseMessage response = await _adminClient.GetAsync(Prefix, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        IReadOnlyList<ProductResponse>? items =
            await response.Content.ReadFromJsonAsync<IReadOnlyList<ProductResponse>>(TestContext.Current.CancellationToken);
        items!.Count.ShouldBe(1);
        items[0].Sku.ShouldBe("API");
    }

    [Fact]
    public async Task ListPublishedProducts_Anonymous_Returns401()
    {
        HttpResponseMessage response = await _anonClient.GetAsync(Prefix, TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ListPublishedProducts_WithoutAdminRole_Returns403()
    {
        HttpResponseMessage response = await _userClient.GetAsync(Prefix, TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // ── Get by id ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetById_WhenFound_Returns200()
    {
        var p = Product.Create(TestProductId, "STORAGE", "Storage", ProductType.Metered, "GB");
        _reader.GetByIdAsync(TestProductId, Arg.Any<CancellationToken>()).Returns(p);

        HttpResponseMessage response = await _adminClient.GetAsync($"{Prefix}/{TestProductId}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        ProductResponse? body = await response.Content.ReadFromJsonAsync<ProductResponse>(TestContext.Current.CancellationToken);
        body!.Sku.ShouldBe("STORAGE");
    }

    [Fact]
    public async Task GetById_WhenMissing_Returns404()
    {
        _reader.GetByIdAsync(TestProductId, Arg.Any<CancellationToken>()).Returns((Product?)null);

        HttpResponseMessage response = await _adminClient.GetAsync($"{Prefix}/{TestProductId}", TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // ── Get by SKU ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetBySku_WhenFound_Returns200()
    {
        var p = Product.Create(TestProductId, "SEAT", "Seat", ProductType.Service, "seat");
        _reader.GetBySkuAsync("SEAT", Arg.Any<CancellationToken>()).Returns(p);

        HttpResponseMessage response = await _adminClient.GetAsync($"{Prefix}/by-sku/SEAT", TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // ── Create ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_WithValidPayload_Returns201()
    {
        ProductCreateRequest payload = new("API", "API Calls", "Metered", "call", "Per-call API metering");

        HttpResponseMessage response = await _adminClient.PostAsJsonAsync(Prefix, payload, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        await _writer.Received(1).AddAsync(
            Arg.Is<Product>(p => p.Id == GeneratedId && p.Sku == "API" && p.Type == ProductType.Metered),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_WithInvalidType_Returns400()
    {
        ProductCreateRequest payload = new("API", "API Calls", "NotARealType", "call");

        HttpResponseMessage response = await _adminClient.PostAsJsonAsync(Prefix, payload, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await _writer.DidNotReceiveWithAnyArgs().AddAsync(default!, TestContext.Current.CancellationToken);
    }

    // ── Update ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_WhenDraft_Returns200()
    {
        var p = Product.Create(TestProductId, "API", "Old", ProductType.Metered, "call");
        _reader.GetByIdAsync(TestProductId, Arg.Any<CancellationToken>()).Returns(p);

        ProductUpdateRequest payload = new("New", "Description", "request");
        HttpResponseMessage response = await _adminClient.PutAsJsonAsync($"{Prefix}/{TestProductId}", payload, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        p.Name.ShouldBe("New");
        p.Unit.ShouldBe("request");
    }

    [Fact]
    public async Task Update_WhenPublished_Returns409()
    {
        var p = Product.Create(TestProductId, "API", "Old", ProductType.Metered, "call");
        p.Publish();
        _reader.GetByIdAsync(TestProductId, Arg.Any<CancellationToken>()).Returns(p);

        ProductUpdateRequest payload = new("New", null, "request");
        HttpResponseMessage response = await _adminClient.PutAsJsonAsync($"{Prefix}/{TestProductId}", payload, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Publish_WhenDraft_Returns204()
    {
        var p = Product.Create(TestProductId, "API", "API Calls", ProductType.Metered, "call");
        _reader.GetByIdAsync(TestProductId, Arg.Any<CancellationToken>()).Returns(p);

        HttpResponseMessage response = await _adminClient.PostAsync(
            $"{Prefix}/{TestProductId}/publish",
            content: null,
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        p.LifecycleStatus.ShouldBe(WorkflowLifecycleStatus.Published);
    }

    [Fact]
    public async Task Publish_WhenAlreadyPublished_Returns409()
    {
        var p = Product.Create(TestProductId, "API", "API Calls", ProductType.Metered, "call");
        p.Publish();
        _reader.GetByIdAsync(TestProductId, Arg.Any<CancellationToken>()).Returns(p);

        HttpResponseMessage response = await _adminClient.PostAsync(
            $"{Prefix}/{TestProductId}/publish",
            content: null,
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Archive_WhenPublished_Returns204()
    {
        var p = Product.Create(TestProductId, "API", "API Calls", ProductType.Metered, "call");
        p.Publish();
        _reader.GetByIdAsync(TestProductId, Arg.Any<CancellationToken>()).Returns(p);

        HttpResponseMessage response = await _adminClient.PostAsync(
            $"{Prefix}/{TestProductId}/archive",
            content: null,
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        p.LifecycleStatus.ShouldBe(WorkflowLifecycleStatus.Archived);
    }

    // ── External mappings ────────────────────────────────────────────────────

    [Fact]
    public async Task AddExternalMapping_Returns200_AndAppendsMapping()
    {
        var p = Product.Create(TestProductId, "API", "API Calls", ProductType.Metered, "call");
        _reader.GetByIdAsync(TestProductId, Arg.Any<CancellationToken>()).Returns(p);

        AddProductExternalMappingRequest payload = new("stripe", "prod_abc");
        HttpResponseMessage response = await _adminClient.PostAsJsonAsync(
            $"{Prefix}/{TestProductId}/external-mappings",
            payload,
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        p.ExternalMappings.Count.ShouldBe(1);
        p.ExternalMappings[0].ProviderName.ShouldBe("stripe");
        p.ExternalMappings[0].ExternalId.ShouldBe("prod_abc");
    }

    [Fact]
    public async Task RemoveExternalMapping_WhenMappingExists_Returns204()
    {
        var p = Product.Create(TestProductId, "API", "API Calls", ProductType.Metered, "call");
        var mappingId = Guid.Parse("00000000-0000-0000-0000-000000000ccc");
        p.AddExternalMapping(ProductExternalMapping.Create(mappingId, "stripe", "prod_abc"));
        _reader.GetByIdAsync(TestProductId, Arg.Any<CancellationToken>()).Returns(p);

        HttpResponseMessage response = await _adminClient.DeleteAsync(
            $"{Prefix}/{TestProductId}/external-mappings/{mappingId}",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        p.ExternalMappings.ShouldBeEmpty();
    }

    [Fact]
    public async Task RemoveExternalMapping_WhenMappingMissing_Returns404()
    {
        var p = Product.Create(TestProductId, "API", "API Calls", ProductType.Metered, "call");
        _reader.GetByIdAsync(TestProductId, Arg.Any<CancellationToken>()).Returns(p);

        HttpResponseMessage response = await _adminClient.DeleteAsync(
            $"{Prefix}/{TestProductId}/external-mappings/{Guid.NewGuid()}",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private HttpClient BuildClient(string role)
    {
        HttpClient client = _app.GetTestClient();
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
