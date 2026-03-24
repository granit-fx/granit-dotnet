using Granit.BlobStorage.Options;
using Granit.BlobStorage.Proxy.Internal;
using Granit.BlobStorage.Proxy.Options;
using Granit.MultiTenancy;
using Granit.Timing;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.BlobStorage.Proxy.Tests;

public sealed class ProxyPresignedUrlProviderTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 13, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid TenantId = Guid.NewGuid();

    private readonly IBlobProxyTokenStore _tokenStore = Substitute.For<IBlobProxyTokenStore>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly ICurrentTenant _currentTenant = Substitute.For<ICurrentTenant>();

    private readonly ProxyBlobOptions _options = new()
    {
        BaseUrl = "https://api.example.com",
        RoutePrefix = "/api/blobs",
        MaxUploadBytes = 104_857_600,
    };

    private readonly ProxyPresignedUrlProvider _sut;

    public ProxyPresignedUrlProviderTests()
    {
        _clock.Now.Returns(Now);
        _currentTenant.IsAvailable.Returns(true);
        _currentTenant.Id.Returns(TenantId);

        _tokenStore.CreateAsync(Arg.Any<ProxyTokenEntry>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns("test-token-abc123");

        _sut = new ProxyPresignedUrlProvider(
            _tokenStore,
            Microsoft.Extensions.Options.Options.Create(_options),
            _clock,
            _currentTenant);
    }

    // ── GenerateUploadTicketAsync ────────────────────────────────────────────

    [Fact]
    public async Task GenerateUploadTicketAsync_returns_proxy_upload_url()
    {
        BlobUploadRequest request = new("file.pdf", "application/pdf", 50_000_000);

        PresignedUploadTicket ticket = await _sut.GenerateUploadTicketAsync(
            "my-bucket", "tenants/t1/file.pdf", Guid.NewGuid(), request,
            TimeSpan.FromMinutes(15), TestContext.Current.CancellationToken);

        ticket.UploadUrl.ToString().ShouldBe("https://api.example.com/api/blobs/upload/test-token-abc123");
        ticket.HttpMethod.ShouldBe("PUT");
        ticket.ExpiresAt.ShouldBe(Now.Add(TimeSpan.FromMinutes(15)));
        ticket.RequiredHeaders.ShouldContainKey("Content-Type");
        ticket.RequiredHeaders["Content-Type"].ShouldBe("application/pdf");
    }

    [Fact]
    public async Task GenerateUploadTicketAsync_stores_token_with_correct_entry()
    {
        var blobId = Guid.NewGuid();
        BlobUploadRequest request = new("file.pdf", "application/pdf", 50_000_000);
        var expiry = TimeSpan.FromMinutes(15);

        await _sut.GenerateUploadTicketAsync(
            "bucket", "key", blobId, request, expiry, TestContext.Current.CancellationToken);

        await _tokenStore.Received(1).CreateAsync(
            Arg.Is<ProxyTokenEntry>(e =>
                e.Type == ProxyTokenType.Upload &&
                e.Bucket == "bucket" &&
                e.ObjectKey == "key" &&
                e.BlobId == blobId &&
                e.TenantId == TenantId &&
                e.ContentType == "application/pdf" &&
                e.MaxBytes == 50_000_000),
            expiry,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateUploadTicketAsync_caps_max_bytes_to_proxy_limit()
    {
        // Request allows 500 MB but proxy max is 100 MB — should pick 100 MB.
        BlobUploadRequest request = new("file.bin", "application/octet-stream", 500_000_000);

        await _sut.GenerateUploadTicketAsync(
            "bucket", "key", Guid.NewGuid(), request,
            TimeSpan.FromMinutes(15), TestContext.Current.CancellationToken);

        await _tokenStore.Received(1).CreateAsync(
            Arg.Is<ProxyTokenEntry>(e => e.MaxBytes == 104_857_600),
            Arg.Any<TimeSpan>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateUploadTicketAsync_without_tenant_stores_null_tenantId()
    {
        _currentTenant.IsAvailable.Returns(false);

        BlobUploadRequest request = new("file.pdf", "application/pdf", 10_000_000);

        await _sut.GenerateUploadTicketAsync(
            "bucket", "key", Guid.NewGuid(), request,
            TimeSpan.FromMinutes(15), TestContext.Current.CancellationToken);

        await _tokenStore.Received(1).CreateAsync(
            Arg.Is<ProxyTokenEntry>(e => e.TenantId == null),
            Arg.Any<TimeSpan>(),
            Arg.Any<CancellationToken>());
    }

    // ── GenerateDownloadUrlAsync ─────────────────────────────────────────────

    [Fact]
    public async Task GenerateDownloadUrlAsync_returns_proxy_download_url()
    {
        PresignedDownloadUrl result = await _sut.GenerateDownloadUrlAsync(
            "my-bucket", "tenants/t1/file.pdf", new DownloadUrlOptions(DownloadFileName: "report.pdf"),
            TimeSpan.FromMinutes(5), TestContext.Current.CancellationToken);

        result.Url.ToString().ShouldBe("https://api.example.com/api/blobs/download/test-token-abc123");
        result.ExpiresAt.ShouldBe(Now.Add(TimeSpan.FromMinutes(5)));
    }

    [Fact]
    public async Task GenerateDownloadUrlAsync_stores_download_filename_in_token()
    {
        await _sut.GenerateDownloadUrlAsync(
            "bucket", "key", new DownloadUrlOptions(DownloadFileName: "report.pdf"),
            TimeSpan.FromMinutes(5), TestContext.Current.CancellationToken);

        await _tokenStore.Received(1).CreateAsync(
            Arg.Is<ProxyTokenEntry>(e =>
                e.Type == ProxyTokenType.Download &&
                e.DownloadFileName == "report.pdf" &&
                e.ContentType == null &&
                e.MaxBytes == null),
            Arg.Any<TimeSpan>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateDownloadUrlAsync_with_null_options_stores_null_filename()
    {
        await _sut.GenerateDownloadUrlAsync(
            "bucket", "key", null,
            TimeSpan.FromMinutes(5), TestContext.Current.CancellationToken);

        await _tokenStore.Received(1).CreateAsync(
            Arg.Is<ProxyTokenEntry>(e => e.DownloadFileName == null),
            Arg.Any<TimeSpan>(),
            Arg.Any<CancellationToken>());
    }

    // ── URL format ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Proxy_url_trims_trailing_slashes()
    {
        ProxyBlobOptions options = new()
        {
            BaseUrl = "https://api.example.com/",
            RoutePrefix = "/api/blobs/",
            MaxUploadBytes = 104_857_600,
        };

        ProxyPresignedUrlProvider sut = new(
            _tokenStore,
            Microsoft.Extensions.Options.Options.Create(options),
            _clock,
            _currentTenant);

        BlobUploadRequest request = new("f.pdf", "application/pdf", 10_000_000);
        PresignedUploadTicket ticket = await sut.GenerateUploadTicketAsync(
            "b", "k", Guid.NewGuid(), request,
            TimeSpan.FromMinutes(15), TestContext.Current.CancellationToken);

        ticket.UploadUrl.ToString().ShouldBe("https://api.example.com/api/blobs/upload/test-token-abc123");
    }
}
