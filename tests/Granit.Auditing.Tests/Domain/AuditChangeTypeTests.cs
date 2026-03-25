using Granit.Auditing.Domain;
using Shouldly;
using Xunit;

namespace Granit.Auditing.Tests.Domain;

public sealed class AuditChangeTypeTests
{
    [Theory]
    [InlineData(AuditChangeType.Created, 0)]
    [InlineData(AuditChangeType.Modified, 1)]
    [InlineData(AuditChangeType.Deleted, 2)]
    [InlineData(AuditChangeType.SoftDeleted, 3)]
    public void Values_HaveExpectedNumericValues(AuditChangeType changeType, int expectedValue) => ((int)changeType).ShouldBe(expectedValue);

    [Fact]
    public void HasFourValues()
    {
        AuditChangeType[] values = Enum.GetValues<AuditChangeType>();
        values.Length.ShouldBe(4);
    }

    [Theory]
    [InlineData(AuditChangeType.Created, "Created")]
    [InlineData(AuditChangeType.Modified, "Modified")]
    [InlineData(AuditChangeType.Deleted, "Deleted")]
    [InlineData(AuditChangeType.SoftDeleted, "SoftDeleted")]
    public void ToString_ReturnsExpectedName(AuditChangeType changeType, string expected) => changeType.ToString().ShouldBe(expected);
}
