using Granit.Persistence.EntityFrameworkCore.MultiTenancy;
using Shouldly;
using Xunit;

namespace Granit.Persistence.EntityFrameworkCore.Tests.MultiTenancy;

public sealed class TenantIsolationOptionsTests
{
    [Fact]
    public void Default_Strategy_IsSharedDatabase()
    {
        TenantIsolationOptions options = new();

        options.Strategy.ShouldBe(TenantIsolationStrategy.SharedDatabase);
    }

    [Fact]
    public void SectionName_HasExpectedValue() =>
        TenantIsolationOptions.SectionName.ShouldBe("MultiTenancy:TenantIsolation");
}
