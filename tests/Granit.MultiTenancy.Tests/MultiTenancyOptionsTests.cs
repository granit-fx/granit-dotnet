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

    [Fact]
    public void IsEnabled_CanBeSet()
    {
        MultiTenancyOptions options = new() { IsEnabled = false };

        options.IsEnabled.ShouldBeFalse();
    }

    [Fact]
    public void TenantIdClaimType_CanBeSet()
    {
        MultiTenancyOptions options = new() { TenantIdClaimType = "custom_claim" };

        options.TenantIdClaimType.ShouldBe("custom_claim");
    }

    [Fact]
    public void TenantIdHeaderName_CanBeSet()
    {
        MultiTenancyOptions options = new() { TenantIdHeaderName = "X-Custom-Header" };

        options.TenantIdHeaderName.ShouldBe("X-Custom-Header");
    }
}
