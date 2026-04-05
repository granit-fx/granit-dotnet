using Shouldly;
using Xunit;

namespace Granit.MultiTenancy.EntityFrameworkCore.Tests;

public sealed class MultiTenancyDbPropertiesTests
{
    [Fact]
    public void DefaultTablePrefix_IsTenants() =>
        MultiTenancyDbProperties.DbTablePrefix.ShouldBe("tenants_");

    [Fact]
    public void DefaultSchema_IsNull() =>
        MultiTenancyDbProperties.DbSchema.ShouldBeNull();
}
