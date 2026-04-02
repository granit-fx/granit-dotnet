using Granit.Auditing.Domain;
using Granit.Auditing.Options;
using Shouldly;
using Xunit;

namespace Granit.Auditing.Tests.Options;

public sealed class AuditingOptionsAdditionalTests
{
    [Fact]
    public void SectionName_IsAuditing() => AuditingOptions.SectionName.ShouldBe("Auditing");

    [Fact]
    public void CacheEntryTtl_DefaultIs30Minutes()
    {
        AuditingOptions options = new();
        options.CacheEntryTtl.ShouldBe(TimeSpan.FromMinutes(30));
    }

    [Fact]
    public void CacheEntityQueryTtl_DefaultIs2Minutes()
    {
        AuditingOptions options = new();
        options.CacheEntityQueryTtl.ShouldBe(TimeSpan.FromMinutes(2));
    }

    [Fact]
    public void GetRetention_UnknownCategory_FallsBackToDataMutation()
    {
        AuditingOptions options = new();
        TimeSpan retention = options.GetRetention((AuditCategory)999);

        retention.ShouldBe(options.DataMutationRetention);
    }

    [Fact]
    public void GetRetention_WithCustomValues_ReturnsCustom()
    {
        AuditingOptions options = new()
        {
            ConfigurationChangeRetention = TimeSpan.FromDays(100),
            DataMutationRetention = TimeSpan.FromDays(200),
            DataAccessRetention = TimeSpan.FromDays(30),
            AccessDeniedRetention = TimeSpan.FromDays(400),
        };

        options.GetRetention(AuditCategory.ConfigurationChange).ShouldBe(TimeSpan.FromDays(100));
        options.GetRetention(AuditCategory.DataMutation).ShouldBe(TimeSpan.FromDays(200));
        options.GetRetention(AuditCategory.DataAccess).ShouldBe(TimeSpan.FromDays(30));
        options.GetRetention(AuditCategory.AccessDenied).ShouldBe(TimeSpan.FromDays(400));
    }

    [Fact]
    public void AllProperties_CanBeSet()
    {
        AuditingOptions options = new()
        {
            PersistenceMode = AuditPersistenceMode.Strict,
            EnablePropertyTracking = false,
            CleanupBatchSize = 5000,
            CleanupInterval = TimeSpan.FromHours(12),
            CacheEntryTtl = TimeSpan.FromMinutes(60),
            CacheEntityQueryTtl = TimeSpan.FromMinutes(5),
        };

        options.PersistenceMode.ShouldBe(AuditPersistenceMode.Strict);
        options.EnablePropertyTracking.ShouldBeFalse();
        options.CleanupBatchSize.ShouldBe(5000);
        options.CleanupInterval.ShouldBe(TimeSpan.FromHours(12));
        options.CacheEntryTtl.ShouldBe(TimeSpan.FromMinutes(60));
        options.CacheEntityQueryTtl.ShouldBe(TimeSpan.FromMinutes(5));
    }
}
