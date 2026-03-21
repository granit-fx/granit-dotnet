using Granit.AuditLog.Domain;
using Shouldly;
using Xunit;

namespace Granit.AuditLog.Tests.Domain;

public sealed class AuditLogCategoryTests
{
    [Theory]
    [InlineData(AuditLogCategory.DataMutation, 0)]
    [InlineData(AuditLogCategory.ConfigurationChange, 1)]
    [InlineData(AuditLogCategory.DataAccess, 2)]
    [InlineData(AuditLogCategory.AccessDenied, 3)]
    public void Values_HaveExpectedNumericValues(AuditLogCategory category, int expectedValue) => ((int)category).ShouldBe(expectedValue);

    [Fact]
    public void HasFourValues()
    {
        AuditLogCategory[] values = Enum.GetValues<AuditLogCategory>();
        values.Length.ShouldBe(4);
    }

    [Theory]
    [InlineData(AuditLogCategory.DataMutation, "DataMutation")]
    [InlineData(AuditLogCategory.ConfigurationChange, "ConfigurationChange")]
    [InlineData(AuditLogCategory.DataAccess, "DataAccess")]
    [InlineData(AuditLogCategory.AccessDenied, "AccessDenied")]
    public void ToString_ReturnsExpectedName(AuditLogCategory category, string expected) => category.ToString().ShouldBe(expected);
}
