// =============================================================================
// TenantInfoTests - Unit tests for the TenantInfo record
// =============================================================================

using Shouldly;
using Xunit;

namespace Granit.MultiTenancy.Tests;

public sealed class TenantInfoTests
{
    [Fact]
    public void Constructor_WithIdAndName_SetsProperties()
    {
        var tenantId = Guid.NewGuid();

        TenantInfo info = new(tenantId, "Acme");

        info.Id.ShouldBe(tenantId);
        info.Name.ShouldBe("Acme");
    }

    [Fact]
    public void Constructor_WithIdOnly_NameDefaultsToNull()
    {
        var tenantId = Guid.NewGuid();

        TenantInfo info = new(tenantId);

        info.Id.ShouldBe(tenantId);
        info.Name.ShouldBeNull();
    }

    [Fact]
    public void Constructor_WithNullId_SetsIdToNull()
    {
        TenantInfo info = new(null, "Ghost");

        info.Id.ShouldBeNull();
        info.Name.ShouldBe("Ghost");
    }

    [Fact]
    public void Implements_ITenantInfo()
    {
        var info = new TenantInfo(Guid.NewGuid(), "Test");

        info.ShouldBeAssignableTo<ITenantInfo>();
        ((ITenantInfo)info).Id.ShouldBe(info.Id);
        ((ITenantInfo)info).Name.ShouldBe(info.Name);
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var tenantId = Guid.NewGuid();
        TenantInfo first = new(tenantId, "Acme");
        TenantInfo second = new(tenantId, "Acme");

        first.ShouldBe(second);
        (first == second).ShouldBeTrue();
    }

    [Fact]
    public void Equality_DifferentId_AreNotEqual()
    {
        TenantInfo first = new(Guid.NewGuid(), "Acme");
        TenantInfo second = new(Guid.NewGuid(), "Acme");

        first.ShouldNotBe(second);
    }

    [Fact]
    public void Equality_DifferentName_AreNotEqual()
    {
        var tenantId = Guid.NewGuid();
        TenantInfo first = new(tenantId, "Acme");
        TenantInfo second = new(tenantId, "Other");

        first.ShouldNotBe(second);
    }

    [Fact]
    public void ToString_ContainsIdAndName()
    {
        var tenantId = Guid.NewGuid();
        TenantInfo info = new(tenantId, "Acme");

        string result = info.ToString();

        result.ShouldContain(tenantId.ToString());
        result.ShouldContain("Acme");
    }

    [Fact]
    public void GetHashCode_SameValues_SameHash()
    {
        var tenantId = Guid.NewGuid();
        TenantInfo first = new(tenantId, "Acme");
        TenantInfo second = new(tenantId, "Acme");

        first.GetHashCode().ShouldBe(second.GetHashCode());
    }
}
