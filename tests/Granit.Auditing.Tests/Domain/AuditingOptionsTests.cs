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

        options.MinimumRetention.ShouldBe(TimeSpan.FromDays(1095));
        options.EnablePropertyTracking.ShouldBeTrue();
        options.CleanupBatchSize.ShouldBe(10_000);
        options.PseudonymizeOnErasure.ShouldBeTrue();
        options.PseudonymizationSalt.ShouldBeNull();
        options.Retention[AuditCategory.ConfigurationChange].ShouldBe(TimeSpan.FromDays(2555));
        options.Retention[AuditCategory.DataMutation].ShouldBe(TimeSpan.FromDays(1095));
        options.Retention[AuditCategory.DataAccess].ShouldBe(TimeSpan.FromDays(1095));
        options.Retention[AuditCategory.AccessDenied].ShouldBe(TimeSpan.FromDays(2555));
        options.Retention[AuditCategory.PrivilegedAccess].ShouldBe(TimeSpan.FromDays(2555));
    }

    [Theory]
    [InlineData(AuditCategory.ConfigurationChange, 2555)]
    [InlineData(AuditCategory.DataMutation, 1095)]
    [InlineData(AuditCategory.DataAccess, 1095)]
    [InlineData(AuditCategory.AccessDenied, 2555)]
    [InlineData(AuditCategory.PrivilegedAccess, 2555)]
    public void GetRetention_ReturnsCorrectDefault(AuditCategory category, int expectedDays)
    {
        AuditingOptions options = new();

        TimeSpan retention = options.GetRetention(category);

        retention.ShouldBe(TimeSpan.FromDays(expectedDays));
    }

    [Theory]
    [InlineData(AuditCategory.ConfigurationChange, 2555)]
    [InlineData(AuditCategory.DataMutation, 1095)]
    [InlineData(AuditCategory.DataAccess, 1095)]
    [InlineData(AuditCategory.AccessDenied, 2555)]
    [InlineData(AuditCategory.PrivilegedAccess, 2555)]
    public void GetRetention_CategoryAbsentFromDictionary_FallsBackToBuiltInDefault(AuditCategory category, int expectedDays)
    {
        AuditingOptions options = new();
        options.Retention.Clear();

        TimeSpan retention = options.GetRetention(category);

        retention.ShouldBe(TimeSpan.FromDays(expectedDays));
    }
}
