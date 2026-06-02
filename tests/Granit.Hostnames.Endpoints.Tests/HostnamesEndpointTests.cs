using System.Net;
using System.Net.Http.Json;
using FluentValidation;
using Granit.Hostnames.Contracts;
using Granit.Hostnames.Domain;
using Granit.Hostnames.Endpoints.Dtos;
using Granit.Hostnames.Endpoints.Extensions;
using Granit.Hostnames.Endpoints.Permissions;
using Granit.Hostnames.Endpoints.Validators;
using Granit.Testing.Endpoints;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Hostnames.Endpoints.Tests;

public sealed class HostnamesEndpointTests : IAsyncDisposable
{
    private const string Prefix = "/hostnames";

    private readonly IManagedHostnameReader _reader = Substitute.For<IManagedHostnameReader>();
    private readonly IManagedHostnameWriter _writer = Substitute.For<IManagedHostnameWriter>();
    private readonly IHostnameRegistrationService _service = Substitute.For<IHostnameRegistrationService>();
    private readonly GranitEndpointTestHost _host;
    private readonly HttpClient _authClient;
    private readonly HttpClient _anonClient;

    private static readonly Guid FixedId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid OwnerId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid TenantId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    public HostnamesEndpointTests()
    {
        _host = GranitEndpointTestHost.StartAsync(
            configureServices: services =>
            {
                services.AddAuthorizationBuilder()
                    .AddPolicy(HostnamesPermissions.Hostnames.Read, p => p.RequireAuthenticatedUser())
                    .AddPolicy(HostnamesPermissions.Hostnames.Manage, p => p.RequireAuthenticatedUser())
                    .AddPolicy(HostnamesPermissions.Certificates.Report, p => p.RequireAuthenticatedUser());

                services.AddSingleton(_reader);
                services.AddSingleton(_writer);
                services.AddSingleton(_service);
                services.AddScoped<IValidator<CreateManagedHostnameRequest>,
                    CreateManagedHostnameRequestValidator>();
            },
            configureEndpoints: app => app.MapGranitHostnames())
            .GetAwaiter().GetResult();

        _authClient = _host.CreateAuthenticatedClient();
        _anonClient = _host.CreateAnonymousClient();
    }

    public async ValueTask DisposeAsync()
    {
        _authClient.Dispose();
        _anonClient.Dispose();
        await _host.DisposeAsync().ConfigureAwait(false);
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
        body.Status.ShouldBe("Pending");
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
        ManagedHostname hostname = MakeHostname("new.example.com");
        _service.RegisterAsync("new.example.com", "cms.site", OwnerId, TenantId, false, Arg.Any<CancellationToken>())
            .Returns(new HostnameRegistrationResult(HostnameRegistrationOutcome.Succeeded, hostname));

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
        _service.RegisterAsync("taken.example.com", Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new HostnameRegistrationResult(HostnameRegistrationOutcome.HostAlreadyTaken, null));

        var request = new CreateManagedHostnameRequest(
            "taken.example.com", "cms.site", OwnerId);

        HttpResponseMessage response = await _authClient.PostAsJsonAsync(
            Prefix, request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Create_InvalidFqdn_Returns_400()
    {
        // The endpoint pre-validates the FQDN before calling the service — service is not reached.
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

    // ── POST /{id}/verify-now ─────────────────────────────────────────────────

    [Fact]
    public async Task VerifyNow_Known_Returns_202()
    {
        ManagedHostname hostname = MakeHostname();
        _service.RequestVerificationAsync(FixedId, Arg.Any<CancellationToken>())
            .Returns(new RequestVerificationResult(RequestVerificationOutcome.Succeeded, hostname));

        HttpResponseMessage response = await _authClient.PostAsync(
            $"{Prefix}/{FixedId}/verify-now", null, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task VerifyNow_Unknown_Returns_404()
    {
        _service.RequestVerificationAsync(FixedId, Arg.Any<CancellationToken>())
            .Returns(new RequestVerificationResult(RequestVerificationOutcome.NotFound, null));

        HttpResponseMessage response = await _authClient.PostAsync(
            $"{Prefix}/{FixedId}/verify-now", null, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task VerifyNow_IngressNotConfigured_Returns_409()
    {
        _service.RequestVerificationAsync(FixedId, Arg.Any<CancellationToken>())
            .Returns(new RequestVerificationResult(RequestVerificationOutcome.IngressNotConfigured, null));

        HttpResponseMessage response = await _authClient.PostAsync(
            $"{Prefix}/{FixedId}/verify-now", null, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    // ── POST /{id}/certificate-status ─────────────────────────────────────────

    [Fact]
    public async Task ReportCertificateStatus_Secured_Returns_204_And_Updates_Hostname()
    {
        ManagedHostname hostname = MakeHostname();
        _reader.GetByIdAsync(FixedId, Arg.Any<CancellationToken>()).Returns(hostname);

        var request = new ReportCertificateStatusRequest(
            CertificateStatus.Secured,
            new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero));

        HttpResponseMessage response = await _authClient.PostAsJsonAsync(
            $"{Prefix}/{FixedId}/certificate-status", request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        hostname.CertificateStatus.ShouldBe(CertificateStatus.Secured);
        await _writer.Received(1).UpdateAsync(hostname, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReportCertificateStatus_Error_Returns_204_And_Updates_Hostname()
    {
        ManagedHostname hostname = MakeHostname();
        _reader.GetByIdAsync(FixedId, Arg.Any<CancellationToken>()).Returns(hostname);

        var request = new ReportCertificateStatusRequest(CertificateStatus.Error);

        HttpResponseMessage response = await _authClient.PostAsJsonAsync(
            $"{Prefix}/{FixedId}/certificate-status", request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        hostname.CertificateStatus.ShouldBe(CertificateStatus.Error);
        await _writer.Received(1).UpdateAsync(hostname, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReportCertificateStatus_Unknown_Returns_404()
    {
        _reader.GetByIdAsync(FixedId, Arg.Any<CancellationToken>())
            .Returns((ManagedHostname?)null);

        var request = new ReportCertificateStatusRequest(CertificateStatus.Provisioning);

        HttpResponseMessage response = await _authClient.PostAsJsonAsync(
            $"{Prefix}/{FixedId}/certificate-status", request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ReportCertificateStatus_Unauthenticated_Returns_401()
    {
        var request = new ReportCertificateStatusRequest(CertificateStatus.Provisioning);

        HttpResponseMessage response = await _anonClient.PostAsJsonAsync(
            $"{Prefix}/{FixedId}/certificate-status", request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
