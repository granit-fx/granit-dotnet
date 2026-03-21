using Granit.Persistence.MultiTenancy;
using Shouldly;
using Xunit;

namespace Granit.Persistence.Tests.MultiTenancy;

public sealed class TenantIsolationOptionsTests
{
    [Fact]
    public void Default_Strategy_IsSharedDatabase()
    {
        TenantIsolationOptions options = new();

        options.Strategy.ShouldBe(TenantIsolationStrategy.SharedDatabase);
    }

    [Theory]
    [InlineData(TenantIsolationStrategy.SharedDatabase)]
    [InlineData(TenantIsolationStrategy.DatabasePerTenant)]
    [InlineData(TenantIsolationStrategy.SchemaPerTenant)]
    public void Strategy_SetAndGet_ReturnsAssignedValue(TenantIsolationStrategy strategy)
    {
        TenantIsolationOptions options = new() { Strategy = strategy };

        options.Strategy.ShouldBe(strategy);
    }
}
