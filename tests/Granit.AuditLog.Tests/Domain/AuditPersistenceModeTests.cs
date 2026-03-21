using Granit.AuditLog.Domain;
using Shouldly;
using Xunit;

namespace Granit.AuditLog.Tests.Domain;

public sealed class AuditPersistenceModeTests
{
    [Theory]
    [InlineData(AuditPersistenceMode.Async, 0)]
    [InlineData(AuditPersistenceMode.Strict, 1)]
    public void Values_HaveExpectedNumericValues(AuditPersistenceMode mode, int expectedValue) => ((int)mode).ShouldBe(expectedValue);

    [Fact]
    public void HasTwoValues()
    {
        AuditPersistenceMode[] values = Enum.GetValues<AuditPersistenceMode>();
        values.Length.ShouldBe(2);
    }
}
