using Granit.Documents.Domain;
using Shouldly;
using Xunit;

namespace Granit.Documents.Tests.Domain;

/// <summary>
/// Aggregate-level tests for <see cref="TenantStorageQuota"/>. Production write path
/// goes through atomic SQL — see the EF Core service for round-trip behaviour.
/// </summary>
public sealed class TenantStorageQuotaTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 10, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid TenantId = Guid.NewGuid();

    [Fact]
    public void Create_SeedsZeroUsage()
    {
        var quota = TenantStorageQuota.Create(Guid.NewGuid(), TenantId, 1024, Now);

        quota.LimitBytes.ShouldBe(1024);
        quota.UsageBytes.ShouldBe(0);
        quota.UpdatedAt.ShouldBe(Now);
        quota.TenantId.ShouldBe(TenantId);
    }

    [Fact]
    public void Create_NonPositiveLimit_Throws() =>
        Should.Throw<ArgumentOutOfRangeException>(() =>
            TenantStorageQuota.Create(Guid.NewGuid(), TenantId, 0, Now));

    [Fact]
    public void Increment_AccumulatesUsageAndStampsUpdatedAt()
    {
        var quota = TenantStorageQuota.Create(Guid.NewGuid(), TenantId, 1024, Now);

        quota.Increment(300, Now.AddSeconds(1));
        quota.Increment(200, Now.AddSeconds(2));

        quota.UsageBytes.ShouldBe(500);
        quota.UpdatedAt.ShouldBe(Now.AddSeconds(2));
    }

    [Fact]
    public void Increment_NegativeDelta_Throws() =>
        Should.Throw<ArgumentOutOfRangeException>(() =>
            TenantStorageQuota.Create(Guid.NewGuid(), TenantId, 1024, Now).Increment(-1, Now));

    [Fact]
    public void Decrement_ClampsAtZero_AbsorbsBookkeepingDrift()
    {
        var quota = TenantStorageQuota.Create(Guid.NewGuid(), TenantId, 1024, Now);
        quota.Increment(100, Now);

        quota.Decrement(500, Now.AddSeconds(1));

        quota.UsageBytes.ShouldBe(0);
    }

    [Fact]
    public void SetLimit_ReplacesLimitAndStampsUpdatedAt()
    {
        var quota = TenantStorageQuota.Create(Guid.NewGuid(), TenantId, 1024, Now);

        quota.SetLimit(2048, Now.AddSeconds(1));

        quota.LimitBytes.ShouldBe(2048);
        quota.UpdatedAt.ShouldBe(Now.AddSeconds(1));
    }

    [Fact]
    public void SetLimit_NonPositive_Throws() =>
        Should.Throw<ArgumentOutOfRangeException>(() =>
            TenantStorageQuota.Create(Guid.NewGuid(), TenantId, 1024, Now).SetLimit(0, Now));
}
