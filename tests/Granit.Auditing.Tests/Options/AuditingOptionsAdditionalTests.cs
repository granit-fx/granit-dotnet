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
    public void GetRetention_UnknownCategory_FallsBackToIsoFloorDefault()
    {
        AuditingOptions options = new();
        TimeSpan retention = options.GetRetention((AuditCategory)999);

        retention.ShouldBe(TimeSpan.FromDays(1095));
    }

    [Fact]
    public void GetRetention_WithCustomValues_ReturnsCustom()
    {
        AuditingOptions options = new()
        {
            Retention =
            {
                [AuditCategory.ConfigurationChange] = TimeSpan.FromDays(100),
                [AuditCategory.DataMutation] = TimeSpan.FromDays(200),
                [AuditCategory.DataAccess] = TimeSpan.FromDays(30),
                [AuditCategory.AccessDenied] = TimeSpan.FromDays(400),
            },
        };

        options.GetRetention(AuditCategory.ConfigurationChange).ShouldBe(TimeSpan.FromDays(100));
        options.GetRetention(AuditCategory.DataMutation).ShouldBe(TimeSpan.FromDays(200));
        options.GetRetention(AuditCategory.DataAccess).ShouldBe(TimeSpan.FromDays(30));
        options.GetRetention(AuditCategory.AccessDenied).ShouldBe(TimeSpan.FromDays(400));
    }
}
