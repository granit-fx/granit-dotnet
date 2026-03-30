using Granit.Persistence.EntityFrameworkCore.MultiTenancy;
using Shouldly;
using Xunit;

namespace Granit.Persistence.EntityFrameworkCore.Tests.MultiTenancy;

public sealed class TenantSchemaOptionsTests
{
    [Fact]
    public void SectionName_HasExpectedValue() => TenantSchemaOptions.SectionName.ShouldBe("TenantSchema");

    [Fact]
    public void Default_NamingConvention_IsTenantId()
    {
        TenantSchemaOptions options = new();

        options.NamingConvention.ShouldBe(TenantSchemaNamingConvention.TenantId);
    }

    [Fact]
    public void Default_Prefix_IsTenant_()
    {
        TenantSchemaOptions options = new();

        options.Prefix.ShouldBe("tenant_");
    }

    [Fact]
    public void NamingConvention_SetAndGet_ReturnsAssignedValue()
    {
        TenantSchemaOptions options = new()
        {
            NamingConvention = TenantSchemaNamingConvention.Custom,
        };

        options.NamingConvention.ShouldBe(TenantSchemaNamingConvention.Custom);
    }

    [Fact]
    public void Prefix_SetAndGet_ReturnsAssignedValue()
    {
        TenantSchemaOptions options = new() { Prefix = "t_" };

        options.Prefix.ShouldBe("t_");
    }
}
