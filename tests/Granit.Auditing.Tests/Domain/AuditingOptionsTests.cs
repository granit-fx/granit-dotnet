using Granit.Auditing.Domain;
using Granit.Auditing.Options;
using Shouldly;
using Xunit;

namespace Granit.Auditing.Tests.Domain;

public sealed class AuditingOptionsTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        AuditingOptions options = new();

        options.PersistenceMode.ShouldBe(AuditPersistenceMode.Async);
        options.EnablePropertyTracking.ShouldBeTrue();
        options.CleanupBatchSize.ShouldBe(10_000);
        options.CleanupInterval.ShouldBe(TimeSpan.FromHours(24));
        options.ConfigurationChangeRetention.ShouldBe(TimeSpan.FromDays(2555));
        options.DataMutationRetention.ShouldBe(TimeSpan.FromDays(365));
        options.DataAccessRetention.ShouldBe(TimeSpan.FromDays(90));
        options.AccessDeniedRetention.ShouldBe(TimeSpan.FromDays(2555));
    }

    [Theory]
    [InlineData(AuditCategory.ConfigurationChange, 2555)]
    [InlineData(AuditCategory.DataMutation, 365)]
    [InlineData(AuditCategory.DataAccess, 90)]
    [InlineData(AuditCategory.AccessDenied, 2555)]
    public void GetRetention_ReturnsCorrectDefault(AuditCategory category, int expectedDays)
    {
        AuditingOptions options = new();

        TimeSpan retention = options.GetRetention(category);

        retention.ShouldBe(TimeSpan.FromDays(expectedDays));
    }
}
