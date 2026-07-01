using System.Net;
using Granit.BlobStorage.Domain;
using Granit.BlobStorage.Endpoints.Extensions;
using Granit.BlobStorage.Endpoints.Permissions;
using Granit.BlobStorage.Options;
using Granit.MultiTenancy;
using Granit.RateLimiting.Extensions;
using Granit.RateLimiting.Options;
using Granit.Testing.Endpoints;
using Granit.Timing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.BlobStorage.Endpoints.Tests;

/// <summary>
/// HTTP-level tests for the <c>GET /blobs/{id}/download</c> endpoint driven through
/// <see cref="GranitEndpointTestHost"/> with substituted stores. This endpoint resolves the blob
/// by identifier alone (no container query parameter) and, honouring the module's Direct-to-Cloud
/// contract, issues a 302 redirect to a fresh pre-signed URL rather than streaming the bytes — so it
/// is consumable directly as an <c>&lt;img src&gt;</c> over the cookie/BFF session.
/// </summary>
public sealed class BlobDownloadEndpointHttpTests
{
    private static readonly Guid BlobId = Guid.Parse("b10b0000-0000-0000-0000-00000000da7a");
    private static readonly DateTimeOffset Now = new(2026, 7, 1, 12, 0, 0, TimeSpan.Zero);
    private const string Container = "medical-images";

    private static string Route => $"/blob-storage/blobs/{BlobId}/download";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly IBlobDescriptorReader _reader = Substitute.For<IBlobDescriptorReader>();
    private readonly IBlobStorage _storage = Substitute.For<IBlobStorage>();
    private readonly IClock _clock = Substitute.For<IClock>();

    public BlobDownloadEndpointHttpTests() => _clock.Now.Returns(Now);

    private Task<GranitEndpointTestHost> StartAsync(int? downloadPermitLimit = null) =>
        GranitEndpointTestHost.StartAsync(
            configureServices: services =>
            {
                services.AddAuthorizationBuilder()
                    .AddPolicy(BlobStoragePermissions.Administration.Read, p =>
                        p.RequireClaim(TestAuthHandler.PermissionClaimType, BlobStoragePermissions.Administration.Read))
                    .AddPolicy(BlobStoragePermissions.Administration.Manage, p =>
                        p.RequireClaim(TestAuthHandler.PermissionClaimType, BlobStoragePermissions.Administration.Manage));

                services.AddSingleton(_reader);
                services.AddSingleton(_storage);
                services.AddSingleton(_clock);

                // The endpoint carries .RequireGranitRateLimiting; its filter resolves the
                // TenantPartitionedRateLimiter, so the service must be registered even when no
                // policy is configured (CheckAsync then no-ops).
                services.AddMetrics();
                services.AddSingleton(TimeProvider.System);
                services.AddSingleton(Substitute.For<ICurrentTenant>());
                services.AddSingleton(Substitute.For<Granit.Users.ICurrentUserService>());
                services.AddGranitRateLimiting(options =>
                {
                    if (downloadPermitLimit is int limit)
                    {
                        options.Policies[BlobStorageRateLimitPolicies.Download] =
                            new RateLimitPolicyOptions { PermitLimit = limit };
                    }
                });
            },
            configureEndpoints: app => app.MapGranitBlobStorage());

    // TestServer's client does not chase redirects (unlike WebApplicationFactory), so the 302 is
    // surfaced verbatim for inspection.
    private static HttpClient ReaderClient(GranitEndpointTestHost host) =>
        host.CreateClientWithPermissions(BlobStoragePermissions.Administration.Read);

    [Fact]
    public async Task Valid_blob_redirects_to_a_fresh_presigned_url()
    {
        _reader.FindAsync(BlobId, Arg.Any<CancellationToken>()).Returns(ValidDescriptor());
        Uri target = new("https://cdn.example.test/medical-images/radio.jpg?sig=abc123");
        _storage
            .CreateDownloadUrlAsync(Container, BlobId, Arg.Any<DownloadUrlOptions?>(), Arg.Any<CancellationToken>())
            .Returns(new PresignedDownloadUrl(target, Now.AddMinutes(5)));

        await using GranitEndpointTestHost host = await StartAsync();
        using HttpClient client = ReaderClient(host);

        HttpResponseMessage response = await client.GetAsync(Route, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Found);
        response.Headers.Location.ShouldBe(target);

        // The 302 is cacheable but only up to shortly before the pre-signed URL expires (5 min here,
        // minus the 30 s safety margin) so a browser never follows a cached, already-stale link.
        response.Headers.CacheControl!.Private.ShouldBeTrue();
        response.Headers.CacheControl.MaxAge!.Value.ShouldBeGreaterThan(TimeSpan.Zero);
        response.Headers.CacheControl.MaxAge.Value.ShouldBeLessThan(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public async Task Pending_blob_is_not_served_and_returns_404()
    {
        _reader.FindAsync(BlobId, Arg.Any<CancellationToken>()).Returns(PendingDescriptor());

        await using GranitEndpointTestHost host = await StartAsync();
        using HttpClient client = ReaderClient(host);

        HttpResponseMessage response = await client.GetAsync(Route, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        await _storage.DidNotReceiveWithAnyArgs()
            .CreateDownloadUrlAsync(default!, default, default, Ct);
    }

    [Fact]
    public async Task Missing_blob_returns_404()
    {
        _reader.FindAsync(BlobId, Arg.Any<CancellationToken>()).Returns((BlobDescriptor?)null);

        await using GranitEndpointTestHost host = await StartAsync();
        using HttpClient client = ReaderClient(host);

        HttpResponseMessage response = await client.GetAsync(Route, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Anonymous_caller_is_unauthorized()
    {
        await using GranitEndpointTestHost host = await StartAsync();
        using HttpClient client = host.CreateAnonymousClient();

        HttpResponseMessage response = await client.GetAsync(Route, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Authenticated_caller_without_read_permission_is_forbidden()
    {
        await using GranitEndpointTestHost host = await StartAsync();
        using HttpClient client = host.CreateClientWithPermissions("BlobStorage.Administration.Other");

        HttpResponseMessage response = await client.GetAsync(Route, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Exceeding_the_download_rate_limit_returns_429()
    {
        _reader.FindAsync(BlobId, Arg.Any<CancellationToken>()).Returns(ValidDescriptor());
        _storage
            .CreateDownloadUrlAsync(Container, BlobId, Arg.Any<DownloadUrlOptions?>(), Arg.Any<CancellationToken>())
            .Returns(new PresignedDownloadUrl(new Uri("https://cdn.example.test/x"), Now.AddMinutes(5)));

        await using GranitEndpointTestHost host = await StartAsync(downloadPermitLimit: 1);
        using HttpClient client = ReaderClient(host);

        HttpResponseMessage first = await client.GetAsync(Route, Ct);
        HttpResponseMessage second = await client.GetAsync(Route, Ct);

        first.StatusCode.ShouldBe(HttpStatusCode.Found);
        second.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }

    private static BlobDescriptor ValidDescriptor()
    {
        BlobDescriptor descriptor = PendingDescriptor();
        descriptor.MarkAsUploading();
        descriptor.MarkAsValid("image/jpeg", 12_345L, Now);
        return descriptor;
    }

    private static BlobDescriptor PendingDescriptor() => BlobDescriptor.Create(
        BlobId,
        tenantId: null,
        Container,
        objectKey: $"{Container}/radio.jpg",
        new BlobUploadRequest("radio.jpg", "image/jpeg", 10_000_000L),
        Now);
}
