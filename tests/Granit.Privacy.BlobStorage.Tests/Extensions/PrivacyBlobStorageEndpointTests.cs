using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Granit.BlobStorage;
using Granit.BlobStorage.Options;
using Granit.Privacy.BlobStorage.Extensions;
using Granit.Privacy.DataExport;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Privacy.BlobStorage.Tests.Extensions;

public sealed class PrivacyBlobStorageEndpointTests
{
    private static readonly Guid CallerId = Guid.Parse("cccccccc-0000-0000-0000-000000000001");
    private static readonly Guid OtherUserId = Guid.Parse("cccccccc-0000-0000-0000-000000000002");

    [Fact]
    public async Task Download_Completed_Redirects302ToPresignedUrl()
    {
        var requestId = Guid.NewGuid();
        var archiveBlobId = Guid.NewGuid();
        Uri presignedUrl = new($"https://s3.example/archive/{archiveBlobId}?sig=abc");

        IExportRequestTrackerReader tracker = Substitute.For<IExportRequestTrackerReader>();
        tracker.GetStatusAsync(requestId, Arg.Any<CancellationToken>())
            .Returns(new ExportRequestStatus(
                requestId, CallerId, CallerId, ExportRequestState.Completed,
                RequestedAt: DateTimeOffset.UtcNow,
                CompletedAt: DateTimeOffset.UtcNow,
                ArchiveBlobReferenceId: archiveBlobId.ToString(),
                MissingProviders: []));

        IBlobStorage blobStorage = Substitute.For<IBlobStorage>();
        blobStorage.CreateDownloadUrlAsync(
            PrivacyExportContainerNames.FragmentContainer,
            archiveBlobId,
            Arg.Any<DownloadUrlOptions?>(),
            Arg.Any<CancellationToken>())
            .Returns(new PresignedDownloadUrl(presignedUrl, DateTimeOffset.UtcNow.AddMinutes(5)));

        await using TestHarness harness = await TestHarness.CreateAsync(tracker, blobStorage);

        HttpResponseMessage response = await harness.Client.GetAsync(
            $"/privacy/exports/{requestId}/download",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Found);
        response.Headers.Location!.ToString().ShouldBe(presignedUrl.ToString());
    }

    [Fact]
    public async Task Download_PartiallyCompleted_Redirects302()
    {
        var requestId = Guid.NewGuid();
        var archiveBlobId = Guid.NewGuid();

        IExportRequestTrackerReader tracker = Substitute.For<IExportRequestTrackerReader>();
        tracker.GetStatusAsync(requestId, Arg.Any<CancellationToken>())
            .Returns(new ExportRequestStatus(
                requestId, CallerId, CallerId, ExportRequestState.PartiallyCompleted,
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, archiveBlobId.ToString(), ["auditing"]));

        IBlobStorage blobStorage = Substitute.For<IBlobStorage>();
        blobStorage.CreateDownloadUrlAsync(
            Arg.Any<string>(), archiveBlobId, Arg.Any<DownloadUrlOptions?>(), Arg.Any<CancellationToken>())
            .Returns(new PresignedDownloadUrl(new Uri("https://s3.example/partial"), DateTimeOffset.UtcNow.AddMinutes(5)));

        await using TestHarness harness = await TestHarness.CreateAsync(tracker, blobStorage);

        HttpResponseMessage response = await harness.Client.GetAsync(
            $"/privacy/exports/{requestId}/download",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Found);
    }

    [Fact]
    public async Task Download_UnknownRequest_Returns404()
    {
        var requestId = Guid.NewGuid();
        IExportRequestTrackerReader tracker = Substitute.For<IExportRequestTrackerReader>();
        tracker.GetStatusAsync(requestId, Arg.Any<CancellationToken>()).Returns((ExportRequestStatus?)null);

        await using TestHarness harness = await TestHarness.CreateAsync(tracker, Substitute.For<IBlobStorage>());

        HttpResponseMessage response = await harness.Client.GetAsync(
            $"/privacy/exports/{requestId}/download",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Download_OtherUsersRequest_Returns404()
    {
        var requestId = Guid.NewGuid();
        IExportRequestTrackerReader tracker = Substitute.For<IExportRequestTrackerReader>();
        tracker.GetStatusAsync(requestId, Arg.Any<CancellationToken>())
            .Returns(new ExportRequestStatus(
                requestId, OtherUserId, OtherUserId, ExportRequestState.Completed,
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, Guid.NewGuid().ToString(), []));

        await using TestHarness harness = await TestHarness.CreateAsync(tracker, Substitute.For<IBlobStorage>());

        HttpResponseMessage response = await harness.Client.GetAsync(
            $"/privacy/exports/{requestId}/download",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Download_Pending_Returns409()
    {
        var requestId = Guid.NewGuid();
        IExportRequestTrackerReader tracker = Substitute.For<IExportRequestTrackerReader>();
        tracker.GetStatusAsync(requestId, Arg.Any<CancellationToken>())
            .Returns(new ExportRequestStatus(
                requestId, CallerId, CallerId, ExportRequestState.Pending,
                DateTimeOffset.UtcNow, null, null, []));

        await using TestHarness harness = await TestHarness.CreateAsync(tracker, Substitute.For<IBlobStorage>());

        HttpResponseMessage response = await harness.Client.GetAsync(
            $"/privacy/exports/{requestId}/download",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Download_SizeLimitExceeded_Returns409()
    {
        var requestId = Guid.NewGuid();
        IExportRequestTrackerReader tracker = Substitute.For<IExportRequestTrackerReader>();
        tracker.GetStatusAsync(requestId, Arg.Any<CancellationToken>())
            .Returns(new ExportRequestStatus(
                requestId, CallerId, CallerId, ExportRequestState.SizeLimitExceeded,
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null, []));

        await using TestHarness harness = await TestHarness.CreateAsync(tracker, Substitute.For<IBlobStorage>());

        HttpResponseMessage response = await harness.Client.GetAsync(
            $"/privacy/exports/{requestId}/download",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Download_Anonymous_Returns401()
    {
        var requestId = Guid.NewGuid();
        await using TestHarness harness = await TestHarness.CreateAsync(
            Substitute.For<IExportRequestTrackerReader>(),
            Substitute.For<IBlobStorage>());

        HttpResponseMessage response = await harness.AnonymousClient.GetAsync(
            $"/privacy/exports/{requestId}/download",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private sealed class TestHarness(WebApplication app) : IAsyncDisposable
    {
        public HttpClient Client { get; } = CreateClient(app, authenticated: true);

        public HttpClient AnonymousClient { get; } = CreateClient(app, authenticated: false);

        public static async Task<TestHarness> CreateAsync(
            IExportRequestTrackerReader tracker, IBlobStorage blobStorage)
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Logging.ClearProviders();

            builder.Services
                .AddAuthentication(StubAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, StubAuthHandler>(StubAuthHandler.SchemeName, _ => { });
            builder.Services.AddAuthorization();

            builder.Services.AddSingleton(tracker);
            builder.Services.AddSingleton(blobStorage);

            WebApplication app = builder.Build();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapGranitPrivacyExportDownload();

            await app.StartAsync().ConfigureAwait(false);
            return new TestHarness(app);
        }

        private static HttpClient CreateClient(WebApplication app, bool authenticated)
        {
            HttpClientHandler inner = new();
            _ = inner;
            HttpClient client = app.GetTestClient();
            client.DefaultRequestHeaders.Remove("Authorization");
            if (authenticated)
            {
                client.DefaultRequestHeaders.Add(StubAuthHandler.AuthHeader, CallerId.ToString());
            }
            return client;
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            AnonymousClient.Dispose();
            await app.DisposeAsync().ConfigureAwait(false);
        }
    }

    private sealed class StubAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "Test";
        public const string AuthHeader = "X-Test-User";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(AuthHeader, out Microsoft.Extensions.Primitives.StringValues header))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            Claim[] claims =
            [
                new("sub", header.ToString()),
                new(ClaimTypes.NameIdentifier, header.ToString()),
            ];
            ClaimsIdentity identity = new(claims, SchemeName);
            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
        }
    }
}
