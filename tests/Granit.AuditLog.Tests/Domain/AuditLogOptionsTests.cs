using Granit.AuditLog.Domain;
using Granit.AuditLog.Options;
using Shouldly;
using Xunit;

namespace Granit.AuditLog.Tests.Domain;

public sealed class AuditLogOptionsTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        AuditLogOptions options = new();

        options.PersistenceMode.ShouldBe(AuditPersistenceMode.Async);
        options.EnablePropertyTracking.ShouldBeTrue();
        options.PersistenceBatchSize.ShouldBe(50);
        options.CleanupBatchSize.ShouldBe(10_000);
        options.CleanupInterval.ShouldBe(TimeSpan.FromHours(24));
        options.ConfigurationChangeRetention.ShouldBe(TimeSpan.FromDays(2555));
        options.DataMutationRetention.ShouldBe(TimeSpan.FromDays(365));
        options.DataAccessRetention.ShouldBe(TimeSpan.FromDays(90));
        options.AccessDeniedRetention.ShouldBe(TimeSpan.FromDays(2555));
    }

    [Theory]
    [InlineData(AuditLogCategory.ConfigurationChange, 2555)]
    [InlineData(AuditLogCategory.DataMutation, 365)]
    [InlineData(AuditLogCategory.DataAccess, 90)]
    [InlineData(AuditLogCategory.AccessDenied, 2555)]
    public void GetRetention_ReturnsCorrectDefault(AuditLogCategory category, int expectedDays)
    {
        AuditLogOptions options = new();

        TimeSpan retention = options.GetRetention(category);

        retention.ShouldBe(TimeSpan.FromDays(expectedDays));
    }
}
