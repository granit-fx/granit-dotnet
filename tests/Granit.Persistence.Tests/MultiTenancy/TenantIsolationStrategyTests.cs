using Granit.Persistence.MultiTenancy;
using Shouldly;
using Xunit;

namespace Granit.Persistence.Tests.MultiTenancy;

public sealed class TenantIsolationStrategyTests
{
    [Fact]
    public void Enum_ContainsThreeValues()
    {
        TenantIsolationStrategy[] values = Enum.GetValues<TenantIsolationStrategy>();

        values.Length.ShouldBe(3);
    }

    [Fact]
    public void SharedDatabase_IsDefaultValue()
    {
        TenantIsolationStrategy defaultValue = default;

        defaultValue.ShouldBe(TenantIsolationStrategy.SharedDatabase);
    }

    [Theory]
    [InlineData(TenantIsolationStrategy.SharedDatabase)]
    [InlineData(TenantIsolationStrategy.DatabasePerTenant)]
    [InlineData(TenantIsolationStrategy.SchemaPerTenant)]
    public void Values_AreDefined(TenantIsolationStrategy strategy) => Enum.IsDefined(strategy).ShouldBeTrue();
}
