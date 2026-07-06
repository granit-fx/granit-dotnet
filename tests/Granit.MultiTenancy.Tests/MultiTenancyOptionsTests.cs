// =============================================================================
// MultiTenancyOptionsTests - Unit tests for configuration options defaults
// =============================================================================

using Granit.MultiTenancy.Options;
using Shouldly;
using Xunit;

namespace Granit.MultiTenancy.Tests;

public sealed class MultiTenancyOptionsTests
{
    [Fact]
    public void SectionName_IsMultiTenancy() => MultiTenancyOptions.SectionName.ShouldBe("MultiTenancy");

    [Fact]
    public void IsEnabled_DefaultsToTrue()
    {
        MultiTenancyOptions options = new();

        options.IsEnabled.ShouldBeTrue();
    }

    [Fact]
    public void TenantIdClaimType_DefaultsToTenantId()
    {
        MultiTenancyOptions options = new();

        options.TenantIdClaimType.ShouldBe("tenant_id");
    }

    [Fact]
    public void TenantIdHeaderName_DefaultsToXTenantId()
    {
        MultiTenancyOptions options = new();

        options.TenantIdHeaderName.ShouldBe("X-Tenant-Id");
    }
}
