using System.Net;
using System.Net.Http.Json;
using FluentValidation;
using Granit.Guids;
using Granit.Hostnames.Contracts;
using Granit.Hostnames.Domain;
using Granit.Hostnames.Endpoints.Dtos;
using Granit.Hostnames.Endpoints.Extensions;
using Granit.Hostnames.Endpoints.Permissions;
using Granit.Hostnames.Endpoints.Validators;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Hostnames.Endpoints.Tests;

public sealed class HostnamesEndpointTests : IAsyncDisposable
{
    private const string AuthRole = "authenticated";
    private const string Prefix = "/hostnames";

    private readonly IManagedHostnameReader _reader = Substitute.For<IManagedHostnameReader>();
    private readonly IManagedHostnameWriter _writer = Substitute.For<IManagedHostnameWriter>();
    private readonly IGuidGenerator _guids = Substitute.For<IGuidGenerator>();
    private readonly WebApplication _app;
    private readonly HttpClient _authClient;
    private readonly HttpClient _anonClient;

    private static readonly Guid FixedId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid OwnerId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid TenantId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    public HostnamesEndpointTests()
    {
        _guids.Create().Returns(FixedId);

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services
            .AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthHandler.SchemeName, _ => { });

        builder.Services.AddAuthorizationBuilder()
            .AddPolicy(HostnamesPermissions.Hostnames.Read, p => p.RequireAuthenticatedUser())
            .AddPolicy(HostnamesPermissions.Hostnames.Manage, p => p.RequireAuthenticatedUser());

        builder.Services.AddSingleton(_reader);
        builder.Services.AddSingleton(_writer);
        builder.Services.AddSingleton(_guids);
        builder.Services.AddScoped<IValidator<CreateManagedHostnameRequest>,
            CreateManagedHostnameRequestValidator>();

        _app = builder.Build();
        _app.MapGranitHostnames();
        _app.StartAsync().GetAwaiter().GetResult();

        _authClient = BuildClient(AuthRole);
        _anonClient = _app.GetTestClient();
    }

    public async ValueTask DisposeAsync()
    {
        _authClient.Dispose();
        _anonClient.Dispose();
        await _app.DisposeAsync();
    }

    private HttpClient BuildClient(string role)
    {
        HttpClient client = _app.GetTestClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, role);
        return client;
    }

    private static ManagedHostname MakeHostname(
        string host = "acme.com",
        bool isPrimary = false) =>
        ManagedHostname.Create(FixedId, host, "cms.site", OwnerId, TenantId, isPrimary);

    // ── GET /{id} ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetById_Known_Returns_200_With_Response()
    {
        ManagedHostname hostname = MakeHostname(isPrimary: true);
        _reader.GetByIdAsync(FixedId, Arg.Any<CancellationToken>()).Returns(hostname);

        HttpResponseMessage response = await _authClient.GetAsync(
            $"{Prefix}/{FixedId}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        ManagedHostnameResponse? body = await response.Content
            .ReadFromJsonAsync<ManagedHostnameResponse>(TestContext.Current.CancellationToken);

        body.ShouldNotBeNull();
        body.Host.ShouldBe("acme.com");
        body.IsPrimary.ShouldBeTrue();
        body.Status.ShouldBe("Active");
    }

    [Fact]
    public async Task GetById_Unknown_Returns_404()
    {
        _reader.GetByIdAsync(FixedId, Arg.Any<CancellationToken>())
            .Returns((ManagedHostname?)null);

        HttpResponseMessage response = await _authClient.GetAsync(
            $"{Prefix}/{FixedId}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetById_Unauthenticated_Returns_401()
    {
        HttpResponseMessage response = await _anonClient.GetAsync(
            $"{Prefix}/{FixedId}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ── GET / (list by owner) ─────────────────────────────────────────────────

    [Fact]
    public async Task ListByOwner_Returns_200_With_List()
    {
        _reader.ListByOwnerAsync("cms.site", OwnerId, Arg.Any<CancellationToken>())
            .Returns([MakeHostname(), MakeHostname("alias.acme.com")]);

        HttpResponseMessage response = await _authClient.GetAsync(
            $"{Prefix}?ownerType=cms.site&ownerId={OwnerId}",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        List<ManagedHostnameResponse>? body = await response.Content
            .ReadFromJsonAsync<List<ManagedHostnameResponse>>(TestContext.Current.CancellationToken);

        body.ShouldNotBeNull();
        body.Count.ShouldBe(2);
    }

    // ── GET /availability ─────────────────────────────────────────────────────

    [Fact]
    public async Task Availability_FreeHost_Returns_Available_True()
    {
        _reader.FindByHostAsync("free.example.com", Arg.Any<CancellationToken>())
            .Returns((ManagedHostname?)null);

        HttpResponseMessage response = await _authClient.GetAsync(
            $"{Prefix}/availability?host=free.example.com",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        HostnameAvailabilityResponse? body = await response.Content
            .ReadFromJsonAsync<HostnameAvailabilityResponse>(TestContext.Current.CancellationToken);

        body.ShouldNotBeNull();
        body.IsAvailable.ShouldBeTrue();
        body.Host.ShouldBe("free.example.com");
    }

    [Fact]
    public async Task Availability_TakenHost_Returns_Available_False()
    {
        _reader.FindByHostAsync("taken.example.com", Arg.Any<CancellationToken>())
            .Returns(MakeHostname("taken.example.com"));

        HttpResponseMessage response = await _authClient.GetAsync(
            $"{Prefix}/availability?host=taken.example.com",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        HostnameAvailabilityResponse? body = await response.Content
            .ReadFromJsonAsync<HostnameAvailabilityResponse>(TestContext.Current.CancellationToken);

        body.ShouldNotBeNull();
        body.IsAvailable.ShouldBeFalse();
    }

    [Fact]
    public async Task Availability_InvalidFqdn_Returns_400()
    {
        HttpResponseMessage response = await _authClient.GetAsync(
            $"{Prefix}/availability?host=not!!valid",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // ── POST / (create) ───────────────────────────────────────────────────────

    [Fact]
    public async Task Create_Valid_Returns_201_With_Location()
    {
        _reader.FindByHostAsync("new.example.com", Arg.Any<CancellationToken>())
            .Returns((ManagedHostname?)null);

        var request = new CreateManagedHostnameRequest(
            "new.example.com", "cms.site", OwnerId, TenantId);

        HttpResponseMessage response = await _authClient.PostAsJsonAsync(
            Prefix, request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.Location.ShouldNotBeNull();

        ManagedHostnameResponse? body = await response.Content
            .ReadFromJsonAsync<ManagedHostnameResponse>(TestContext.Current.CancellationToken);

        body.ShouldNotBeNull();
        body.Host.ShouldBe("new.example.com");
        body.OwnerType.ShouldBe("cms.site");
    }

    [Fact]
    public async Task Create_DuplicateHost_Returns_409()
    {
        _reader.FindByHostAsync("taken.example.com", Arg.Any<CancellationToken>())
            .Returns(MakeHostname("taken.example.com"));

        var request = new CreateManagedHostnameRequest(
            "taken.example.com", "cms.site", OwnerId);

        HttpResponseMessage response = await _authClient.PostAsJsonAsync(
            Prefix, request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Create_InvalidFqdn_Returns_400()
    {
        var request = new CreateManagedHostnameRequest(
            "not!!valid", "cms.site", OwnerId);

        HttpResponseMessage response = await _authClient.PostAsJsonAsync(
            Prefix, request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_EmptyOwnerType_Returns_422()
    {
        var request = new CreateManagedHostnameRequest(
            "valid.example.com", "", OwnerId);

        HttpResponseMessage response = await _authClient.PostAsJsonAsync(
            Prefix, request, TestContext.Current.CancellationToken);

        // FluentValidation auto-filter returns 422 Unprocessable Entity for structural violations.
        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    // ── DELETE /{id} ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_Known_Returns_204()
    {
        ManagedHostname hostname = MakeHostname();
        _reader.GetByIdAsync(FixedId, Arg.Any<CancellationToken>()).Returns(hostname);

        HttpResponseMessage response = await _authClient.DeleteAsync(
            $"{Prefix}/{FixedId}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await _writer.Received(1).DeleteAsync(hostname, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_Unknown_Returns_404()
    {
        _reader.GetByIdAsync(FixedId, Arg.Any<CancellationToken>())
            .Returns((ManagedHostname?)null);

        HttpResponseMessage response = await _authClient.DeleteAsync(
            $"{Prefix}/{FixedId}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // ── POST /{id}/primary (set primary) ──────────────────────────────────────

    [Fact]
    public async Task SetPrimary_Known_Returns_204_And_Calls_SetPrimary()
    {
        ManagedHostname hostname = MakeHostname();
        _reader.GetByIdAsync(FixedId, Arg.Any<CancellationToken>()).Returns(hostname);

        HttpResponseMessage response = await _authClient.PostAsync(
            $"{Prefix}/{FixedId}/primary", null, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        hostname.IsPrimary.ShouldBeTrue();
        await _writer.Received(1).UpdateAsync(hostname, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetPrimary_Unknown_Returns_404()
    {
        _reader.GetByIdAsync(FixedId, Arg.Any<CancellationToken>())
            .Returns((ManagedHostname?)null);

        HttpResponseMessage response = await _authClient.PostAsync(
            $"{Prefix}/{FixedId}/primary", null, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // ── DELETE /{id}/primary (clear primary) ──────────────────────────────────

    [Fact]
    public async Task ClearPrimary_Known_Returns_204_And_Clears_Flag()
    {
        ManagedHostname hostname = MakeHostname(isPrimary: true);
        _reader.GetByIdAsync(FixedId, Arg.Any<CancellationToken>()).Returns(hostname);

        HttpResponseMessage response = await _authClient.DeleteAsync(
            $"{Prefix}/{FixedId}/primary", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        hostname.IsPrimary.ShouldBeFalse();
        await _writer.Received(1).UpdateAsync(hostname, Arg.Any<CancellationToken>());
    }
}
