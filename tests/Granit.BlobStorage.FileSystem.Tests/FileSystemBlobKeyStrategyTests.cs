using Granit.BlobStorage.FileSystem.Internal;
using Granit.BlobStorage.FileSystem.Options;
using Granit.MultiTenancy;
using Granit.Timing;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.BlobStorage.FileSystem.Tests;

public sealed class FileSystemBlobKeyStrategyTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTimeOffset Now = new(2026, 2, 23, 12, 0, 0, TimeSpan.Zero);

    private readonly ICurrentTenant _currentTenant = Substitute.For<ICurrentTenant>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly FileSystemBlobKeyStrategy _sut;

    public FileSystemBlobKeyStrategyTests()
    {
        _currentTenant.IsAvailable.Returns(true);
        _currentTenant.Id.Returns(TenantId);
        _clock.Now.Returns(Now);

        _sut = new FileSystemBlobKeyStrategy(
            _currentTenant,
            _clock,
            Microsoft.Extensions.Options.Options.Create(new FileSystemBlobOptions { BasePath = "/data/blobs" }));
    }

    // ── BuildObjectKey ────────────────────────────────────────────────────────

    [Fact]
    public void BuildObjectKey_ShouldReturnTenantPrefixedKeyWithDateComponents()
    {
        var blobId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        string key = _sut.BuildObjectKey("medical-images", blobId);

        key.ShouldBe($"{TenantId}/medical-images/2026/02/{blobId}");
    }

    [Fact]
    public void BuildObjectKey_ShouldPadMonthWithLeadingZero()
    {
        _clock.Now.Returns(new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero));
        var blobId = Guid.NewGuid();

        string key = _sut.BuildObjectKey("prescriptions", blobId);

        key.ShouldContain("/2026/03/");
    }

    [Fact]
    public void BuildObjectKey_TwoTenantsWithSameFileName_ShouldProduceDifferentKeys()
    {
        var blobId = Guid.NewGuid();
        var tenantB = Guid.Parse("33333333-3333-3333-3333-333333333333");

        FileSystemBlobKeyStrategy strategyB = new(
            BuildTenantSubstitute(tenantB), _clock,
            Microsoft.Extensions.Options.Options.Create(new FileSystemBlobOptions { BasePath = "/data/blobs" }));

        string keyA = _sut.BuildObjectKey("medical-images", blobId);
        string keyB = strategyB.BuildObjectKey("medical-images", blobId);

        keyA.ShouldNotBe(keyB);
        keyA.ShouldStartWith(TenantId.ToString());
        keyB.ShouldStartWith(tenantB.ToString());
    }

    [Fact]
    public void BuildObjectKey_WhenNoActiveTenant_ShouldOmitTenantPrefix()
    {
        _currentTenant.IsAvailable.Returns(false);
        _currentTenant.Id.Returns((Guid?)null);
        var blobId = Guid.NewGuid();

        string key = _sut.BuildObjectKey("docs", blobId);

        key.ShouldStartWith("docs/");
        key.ShouldEndWith(blobId.ToString());
        key.ShouldNotContain(TenantId.ToString());
    }

    // ── ResolveBucketName ─────────────────────────────────────────────────────

    [Fact]
    public void ResolveBucketName_ShouldReturnConfiguredBasePath()
    {
        string bucket = _sut.ResolveBucketName("medical-images");

        bucket.ShouldBe("/data/blobs");
    }

    [Fact]
    public void ResolveBucketName_ShouldReturnSameBasePathRegardlessOfContainer()
    {
        _sut.ResolveBucketName("medical-images").ShouldBe("/data/blobs");
        _sut.ResolveBucketName("prescriptions").ShouldBe("/data/blobs");
        _sut.ResolveBucketName("avatars").ShouldBe("/data/blobs");
    }

    // ── TryExtractTenantId ────────────────────────────────────────────────────

    [Fact]
    public void TryExtractTenantId_WithValidKey_ShouldExtractFirstSegment()
    {
        string objectKey = $"{TenantId}/medical-images/2026/02/some-blob-id";

        bool result = _sut.TryExtractTenantId(objectKey, out string? tenantId);

        result.ShouldBeTrue();
        tenantId.ShouldBe(TenantId.ToString());
    }

    [Fact]
    public void TryExtractTenantId_WithKeyBuiltByStrategy_ShouldRoundtrip()
    {
        var blobId = Guid.NewGuid();
        string objectKey = _sut.BuildObjectKey("medical-images", blobId);

        bool result = _sut.TryExtractTenantId(objectKey, out string? extractedTenantId);

        result.ShouldBeTrue();
        extractedTenantId.ShouldBe(TenantId.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("nokeyslash")]
    public void TryExtractTenantId_WithMalformedKey_ShouldReturnFalse(string malformedKey)
    {
        bool result = _sut.TryExtractTenantId(malformedKey, out string? tenantId);

        result.ShouldBeFalse();
        tenantId.ShouldBeNull();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static ICurrentTenant BuildTenantSubstitute(Guid id)
    {
        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.IsAvailable.Returns(true);
        tenant.Id.Returns(id);
        return tenant;
    }
}
