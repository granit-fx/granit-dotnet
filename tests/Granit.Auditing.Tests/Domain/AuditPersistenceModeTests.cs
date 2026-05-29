using Granit.Auditing.Domain;
using Shouldly;
using Xunit;

namespace Granit.Auditing.Tests.Domain;

public sealed class AuditPersistenceModeTests
{
    [Theory]
    [InlineData(AuditPersistenceMode.Strict, 0)]
    [InlineData(AuditPersistenceMode.Async, 1)]
    public void Values_HaveExpectedNumericValues(AuditPersistenceMode mode, int expectedValue) => ((int)mode).ShouldBe(expectedValue);

    [Fact]
    public void Default_IsStrict_SoUnconfiguredIsDurable() => default(AuditPersistenceMode).ShouldBe(AuditPersistenceMode.Strict);

    [Fact]
    public void HasTwoValues()
    {
        AuditPersistenceMode[] values = Enum.GetValues<AuditPersistenceMode>();
        values.Length.ShouldBe(2);
    }
}
