using Granit.Auditing.Domain;
using Shouldly;
using Xunit;

namespace Granit.Auditing.Tests.Domain;

public sealed class AuditCategoryTests
{
    [Theory]
    [InlineData(AuditCategory.DataMutation, 0)]
    [InlineData(AuditCategory.ConfigurationChange, 1)]
    [InlineData(AuditCategory.DataAccess, 2)]
    [InlineData(AuditCategory.AccessDenied, 3)]
    [InlineData(AuditCategory.PrivilegedAccess, 4)]
    public void Values_HaveExpectedNumericValues(AuditCategory category, int expectedValue) => ((int)category).ShouldBe(expectedValue);

    [Fact]
    public void HasFiveValues()
    {
        AuditCategory[] values = Enum.GetValues<AuditCategory>();
        values.Length.ShouldBe(5);
    }

    [Theory]
    [InlineData(AuditCategory.DataMutation, "DataMutation")]
    [InlineData(AuditCategory.ConfigurationChange, "ConfigurationChange")]
    [InlineData(AuditCategory.DataAccess, "DataAccess")]
    [InlineData(AuditCategory.AccessDenied, "AccessDenied")]
    [InlineData(AuditCategory.PrivilegedAccess, "PrivilegedAccess")]
    public void ToString_ReturnsExpectedName(AuditCategory category, string expected) => category.ToString().ShouldBe(expected);
}
