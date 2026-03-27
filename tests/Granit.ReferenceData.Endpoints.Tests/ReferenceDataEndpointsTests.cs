using System.Net;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Granit.Guids;
using Granit.QueryEngine;
using Granit.ReferenceData.Domain;
using Granit.ReferenceData.Endpoints.Dtos;
using Granit.ReferenceData.Endpoints.Extensions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]

namespace Granit.ReferenceData.Endpoints.Tests;

internal sealed class TestRefEntity : ReferenceDataEntity;

/// <summary>
/// Integration tests for reference data endpoints.
/// Uses a TestServer + NSubstitute mocks for IReferenceDataStoreReader and IReferenceDataStoreWriter.
/// </summary>
public sealed class ReferenceDataEndpointsTests : IAsyncDisposable
{
    private const string AdminRole = "granit-reference-data-admin";
    private const string Prefix = "/reference-data/test-ref-entity";

    private readonly IReferenceDataStoreReader<TestRefEntity> _storeReader =
        Substitute.For<IReferenceDataStoreReader<TestRefEntity>>();
    private readonly IReferenceDataStoreWriter<TestRefEntity> _storeWriter =
        Substitute.For<IReferenceDataStoreWriter<TestRefEntity>>();
    private readonly WebApplication _app;
    private readonly HttpClient _adminClient;
    private readonly HttpClient _anonClient;

    public ReferenceDataEndpointsTests()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services
            .AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthHandler.SchemeName, _ => { });

        builder.Services.AddAuthorizationBuilder()
            .AddPolicy(Permissions.ReferenceDataPermissions.Entries.Read,
                policy => policy.RequireAuthenticatedUser())
            .AddPolicy(Permissions.ReferenceDataPermissions.Entries.Create,
                policy => policy.RequireRole(AdminRole))
            .AddPolicy(Permissions.ReferenceDataPermissions.Entries.Manage,
                policy => policy.RequireRole(AdminRole));
        builder.Services.AddSingleton(_storeReader);
        builder.Services.AddSingleton(_storeWriter);
        builder.Services.AddSingleton<IGuidGenerator>(new SimpleGuidGenerator());

        _app = builder.Build();
        _app.MapReferenceDataEndpoints<TestRefEntity>();
        _app.StartAsync().GetAwaiter().GetResult();

        _adminClient = BuildClient(AdminRole);
        _anonClient = _app.GetTestClient();
    }

    public async ValueTask DisposeAsync() => await _app.DisposeAsync();

    // ── GET / ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAll_Returns200WithList()
    {
        // Arrange
        PagedResult<TestRefEntity> result = new(
            [new TestRefEntity { Code = "BE", LabelEn = "Belgium" }], 1, HasMore: false);
        _storeReader.GetAllAsync(Arg.Any<ReferenceDataQuery?>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        HttpResponseMessage response = await _adminClient.GetAsync(
            Prefix, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        PagedResult<TestRefEntity>? body =
            await response.Content.ReadFromJsonAsync<PagedResult<TestRefEntity>>(
                TestContext.Current.CancellationToken);
        body!.TotalCount.ShouldBe(1);
        body.Items.Count.ShouldBe(1);
        body.Items[0].Code.ShouldBe("BE");
    }

    // ── GET /{code} ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetByCode_WhenExists_Returns200()
    {
        // Arrange
        TestRefEntity entity = new() { Code = "BE", LabelEn = "Belgium" };
        _storeReader.GetByCodeAsync("BE", Arg.Any<CancellationToken>()).Returns(entity);

        // Act
        HttpResponseMessage response = await _adminClient.GetAsync(
            $"{Prefix}/BE", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        TestRefEntity? body = await response.Content.ReadFromJsonAsync<TestRefEntity>(
            TestContext.Current.CancellationToken);
        body!.Code.ShouldBe("BE");
    }

    [Fact]
    public async Task GetByCode_WhenNotFound_Returns404()
    {
        // Arrange
        _storeReader.GetByCodeAsync("ZZ", Arg.Any<CancellationToken>())
            .Returns((TestRefEntity?)null);

        // Act
        HttpResponseMessage response = await _adminClient.GetAsync(
            $"{Prefix}/ZZ", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // ── POST / ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_WithAdminRole_Returns201()
    {
        // Arrange
        ReferenceDataCreateRequest request = new("DE", "Germany");

        // Act
        HttpResponseMessage response = await _adminClient.PostAsJsonAsync(
            Prefix, request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        await _storeWriter.Received(1).CreateAsync(
            Arg.Is<TestRefEntity>(e => e.Code == "DE" && e.LabelEn == "Germany"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_WithTranslationLabels_MapsAllLabels()
    {
        // Arrange
        ReferenceDataCreateRequest request = new(
            "BE", "Belgium",
            LabelFr: "Belgique",
            LabelNl: "België",
            LabelDe: "Belgien",
            LabelEs: "Bélgica",
            LabelIt: "Belgio",
            LabelPt: "Bélgica");

        // Act
        HttpResponseMessage response = await _adminClient.PostAsJsonAsync(
            Prefix, request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        await _storeWriter.Received(1).CreateAsync(
            Arg.Is<TestRefEntity>(e =>
                e.Code == "BE" &&
                e.LabelEn == "Belgium" &&
                e.LabelFr == "Belgique" &&
                e.LabelNl == "België" &&
                e.LabelDe == "Belgien" &&
                e.LabelEs == "Bélgica" &&
                e.LabelIt == "Belgio" &&
                e.LabelPt == "Bélgica"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_WithoutAuth_Returns401()
    {
        // Arrange
        ReferenceDataCreateRequest request = new("DE", "Germany");

        // Act
        HttpResponseMessage response = await _anonClient.PostAsJsonAsync(
            Prefix, request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ── PUT /{code} ────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_WhenExists_Returns200()
    {
        // Arrange
        TestRefEntity existing = new() { Code = "BE", LabelEn = "Belgium" };
        _storeReader.GetByCodeAsync("BE", Arg.Any<CancellationToken>()).Returns(existing);
        ReferenceDataUpdateRequest request = new("Kingdom of Belgium");

        // Act
        HttpResponseMessage response = await _adminClient.PutAsJsonAsync(
            $"{Prefix}/BE", request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await _storeWriter.Received(1).UpdateAsync(
            Arg.Is<TestRefEntity>(e => e.LabelEn == "Kingdom of Belgium"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_WithTranslationLabels_MapsAllLabels()
    {
        // Arrange
        TestRefEntity existing = new() { Code = "BE", LabelEn = "Belgium" };
        _storeReader.GetByCodeAsync("BE", Arg.Any<CancellationToken>()).Returns(existing);
        ReferenceDataUpdateRequest request = new(
            "Kingdom of Belgium",
            LabelFr: "Royaume de Belgique",
            LabelNl: "Koninkrijk België",
            LabelDe: "Königreich Belgien");

        // Act
        HttpResponseMessage response = await _adminClient.PutAsJsonAsync(
            $"{Prefix}/BE", request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await _storeWriter.Received(1).UpdateAsync(
            Arg.Is<TestRefEntity>(e =>
                e.LabelEn == "Kingdom of Belgium" &&
                e.LabelFr == "Royaume de Belgique" &&
                e.LabelNl == "Koninkrijk België" &&
                e.LabelDe == "Königreich Belgien"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_WhenNotFound_Returns404()
    {
        // Arrange
        _storeReader.GetByCodeAsync("ZZ", Arg.Any<CancellationToken>())
            .Returns((TestRefEntity?)null);
        ReferenceDataUpdateRequest request = new("Unknown");

        // Act
        HttpResponseMessage response = await _adminClient.PutAsJsonAsync(
            $"{Prefix}/ZZ", request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // ── DELETE /{code} ──────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_WhenExists_Returns204()
    {
        // Arrange
        TestRefEntity existing = new() { Code = "BE", LabelEn = "Belgium" };
        _storeReader.GetByCodeAsync("BE", Arg.Any<CancellationToken>()).Returns(existing);

        // Act
        HttpResponseMessage response = await _adminClient.DeleteAsync(
            $"{Prefix}/BE", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await _storeWriter.Received(1).SetActiveAsync("BE", false, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_WhenNotFound_Returns404()
    {
        // Arrange
        _storeReader.GetByCodeAsync("ZZ", Arg.Any<CancellationToken>())
            .Returns((TestRefEntity?)null);

        // Act
        HttpResponseMessage response = await _adminClient.DeleteAsync(
            $"{Prefix}/ZZ", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

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
