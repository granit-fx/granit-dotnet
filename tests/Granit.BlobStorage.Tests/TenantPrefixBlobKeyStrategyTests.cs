using Granit.Core.MultiTenancy;
using Granit.Timing;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.BlobStorage.Tests;

public sealed class TenantPrefixBlobKeyStrategyTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 21, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid TestTenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private readonly ICurrentTenant _currentTenant = Substitute.For<ICurrentTenant>();
    private readonly IClock _clock = Substitute.For<IClock>();

    public TenantPrefixBlobKeyStrategyTests()
    {
        _clock.Now.Returns(Now);
    }

    // ── BuildObjectKey ───────────────────────────────────────────────────────

    [Fact]
    public void BuildObjectKey_WithTenant_IncludesTenantPrefix()
    {
        _currentTenant.IsAvailable.Returns(true);
        _currentTenant.Id.Returns(TestTenantId);
        TestableKeyStrategy strategy = new(_currentTenant, _clock);
        var blobId = Guid.NewGuid();

        string key = strategy.BuildObjectKey("medical-images", blobId);

        key.ShouldBe($"{TestTenantId}/medical-images/2026/03/{blobId}");
    }

    [Fact]
    public void BuildObjectKey_WithoutTenant_OmitsTenantPrefix()
    {
        _currentTenant.IsAvailable.Returns(false);
        TestableKeyStrategy strategy = new(_currentTenant, _clock);
        var blobId = Guid.NewGuid();

        string key = strategy.BuildObjectKey("docs", blobId);

        key.ShouldBe($"docs/2026/03/{blobId}");
    }

    [Fact]
    public void BuildObjectKey_WithNullTenantId_OmitsTenantPrefix()
    {
        _currentTenant.IsAvailable.Returns(true);
        _currentTenant.Id.Returns((Guid?)null);
        TestableKeyStrategy strategy = new(_currentTenant, _clock);
        var blobId = Guid.NewGuid();

        string key = strategy.BuildObjectKey("docs", blobId);

        key.ShouldBe($"docs/2026/03/{blobId}");
    }

    [Fact]
    public void BuildObjectKey_IncludesYearAndMonth()
    {
        _currentTenant.IsAvailable.Returns(true);
        _currentTenant.Id.Returns(TestTenantId);
        IClock julyClk = Substitute.For<IClock>();
        julyClk.Now.Returns(new DateTimeOffset(2026, 7, 15, 0, 0, 0, TimeSpan.Zero));
        TestableKeyStrategy strategy = new(_currentTenant, julyClk);
        var blobId = Guid.NewGuid();

        string key = strategy.BuildObjectKey("images", blobId);

        key.ShouldContain("/2026/07/");
    }

    // ── TryExtractTenantId ───────────────────────────────────────────────────

    [Fact]
    public void TryExtractTenantId_WithTenantPrefix_ReturnsTrueAndExtractsTenantId()
    {
        TestableKeyStrategy strategy = new(_currentTenant, _clock);

        bool result = strategy.TryExtractTenantId($"{TestTenantId}/container/2026/03/blob-id", out string? tenantId);

        result.ShouldBeTrue();
        tenantId.ShouldBe(TestTenantId.ToString());
    }

    [Fact]
    public void TryExtractTenantId_WithoutSlash_ReturnsFalse()
    {
        TestableKeyStrategy strategy = new(_currentTenant, _clock);

        bool result = strategy.TryExtractTenantId("no-slash", out string? tenantId);

        result.ShouldBeFalse();
        tenantId.ShouldBeNull();
    }

    [Fact]
    public void TryExtractTenantId_EmptyString_ReturnsFalse()
    {
        TestableKeyStrategy strategy = new(_currentTenant, _clock);

        bool result = strategy.TryExtractTenantId("", out string? tenantId);

        result.ShouldBeFalse();
        tenantId.ShouldBeNull();
    }

    [Fact]
    public void TryExtractTenantId_NullString_ReturnsFalse()
    {
        TestableKeyStrategy strategy = new(_currentTenant, _clock);

        bool result = strategy.TryExtractTenantId(null!, out string? tenantId);

        result.ShouldBeFalse();
        tenantId.ShouldBeNull();
    }

    [Fact]
    public void TryExtractTenantId_LeadingSlash_ReturnsFalse()
    {
        TestableKeyStrategy strategy = new(_currentTenant, _clock);

        bool result = strategy.TryExtractTenantId("/container/2026/03/blob-id", out string? tenantId);

        result.ShouldBeFalse();
        tenantId.ShouldBeNull();
    }

    // ── Test helper ──────────────────────────────────────────────────────────

    private sealed class TestableKeyStrategy(
        ICurrentTenant currentTenant,
        IClock clock) : TenantPrefixBlobKeyStrategy(currentTenant, clock)
    {
        public override string ResolveBucketName(string containerName) => "test-bucket";
    }
}
