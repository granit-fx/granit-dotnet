using Granit.Payments.Domain;
using Shouldly;
using Xunit;

namespace Granit.Payments.Stripe.Tests;

public sealed class ProviderCustomerMappingTests
{
    [Fact]
    public void Create_ShouldSetProperties()
    {
        var tenantId = Guid.NewGuid();

        var mapping = ProviderCustomerMapping.Create(
            Guid.NewGuid(), "stripe", tenantId, "cus_abc123");

        mapping.ProviderName.ShouldBe("stripe");
        mapping.TenantId.ShouldBe(tenantId);
        mapping.ProviderCustomerId.ShouldBe("cus_abc123");
    }

    [Fact]
    public void Create_WithNullProviderName_ShouldThrow()
    {
        Should.Throw<ArgumentException>(() =>
            ProviderCustomerMapping.Create(Guid.NewGuid(), null!, Guid.NewGuid(), "cus_123"));
    }

    [Fact]
    public void Create_WithNullCustomerId_ShouldThrow()
    {
        Should.Throw<ArgumentException>(() =>
            ProviderCustomerMapping.Create(Guid.NewGuid(), "stripe", Guid.NewGuid(), null!));
    }
}
