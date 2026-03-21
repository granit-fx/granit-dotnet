using Granit.AuditLog.Domain;
using Granit.AuditLog.Options;
using Shouldly;
using Xunit;

namespace Granit.AuditLog.Tests.Options;

public sealed class AuditLogOptionsAdditionalTests
{
    [Fact]
    public void SectionName_IsAuditLog() => AuditLogOptions.SectionName.ShouldBe("AuditLog");

    [Fact]
    public void CacheEntryTtl_DefaultIs30Minutes()
    {
        AuditLogOptions options = new();
        options.CacheEntryTtl.ShouldBe(TimeSpan.FromMinutes(30));
    }

    [Fact]
    public void CacheEntityQueryTtl_DefaultIs2Minutes()
    {
        AuditLogOptions options = new();
        options.CacheEntityQueryTtl.ShouldBe(TimeSpan.FromMinutes(2));
    }

    [Fact]
    public void GetRetention_UnknownCategory_FallsBackToDataMutation()
    {
        AuditLogOptions options = new();
        TimeSpan retention = options.GetRetention((AuditLogCategory)999);

        retention.ShouldBe(options.DataMutationRetention);
    }

    [Fact]
    public void GetRetention_WithCustomValues_ReturnsCustom()
    {
        AuditLogOptions options = new()
        {
            ConfigurationChangeRetention = TimeSpan.FromDays(100),
            DataMutationRetention = TimeSpan.FromDays(200),
            DataAccessRetention = TimeSpan.FromDays(30),
            AccessDeniedRetention = TimeSpan.FromDays(400),
        };

        options.GetRetention(AuditLogCategory.ConfigurationChange).ShouldBe(TimeSpan.FromDays(100));
        options.GetRetention(AuditLogCategory.DataMutation).ShouldBe(TimeSpan.FromDays(200));
        options.GetRetention(AuditLogCategory.DataAccess).ShouldBe(TimeSpan.FromDays(30));
        options.GetRetention(AuditLogCategory.AccessDenied).ShouldBe(TimeSpan.FromDays(400));
    }

    [Fact]
    public void AllProperties_CanBeSet()
    {
        AuditLogOptions options = new()
        {
            PersistenceMode = AuditPersistenceMode.Strict,
            EnablePropertyTracking = false,
            PersistenceBatchSize = 100,
            CleanupBatchSize = 5000,
            CleanupInterval = TimeSpan.FromHours(12),
            CacheEntryTtl = TimeSpan.FromMinutes(60),
            CacheEntityQueryTtl = TimeSpan.FromMinutes(5),
        };

        options.PersistenceMode.ShouldBe(AuditPersistenceMode.Strict);
        options.EnablePropertyTracking.ShouldBeFalse();
        options.PersistenceBatchSize.ShouldBe(100);
        options.CleanupBatchSize.ShouldBe(5000);
        options.CleanupInterval.ShouldBe(TimeSpan.FromHours(12));
        options.CacheEntryTtl.ShouldBe(TimeSpan.FromMinutes(60));
        options.CacheEntityQueryTtl.ShouldBe(TimeSpan.FromMinutes(5));
    }
}
