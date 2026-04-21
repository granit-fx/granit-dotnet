using Granit.Identity.Local.AspNetIdentity.Internal;
using Granit.MultiTenancy;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Identity.Local.AspNetIdentity.Tests;

public sealed class TenantAwareRoleLookupNormalizerTests
{
    [Fact]
    public void NormalizeName_WithoutTenant_ReturnsUpperInvariant()
    {
        ICurrentTenant currentTenant = Substitute.For<ICurrentTenant>();
        currentTenant.IsAvailable.Returns(false);

        TenantAwareRoleLookupNormalizer normalizer = new(currentTenant);

        normalizer.NormalizeName("Manager").ShouldBe("MANAGER");
    }

    [Fact]
    public void NormalizeName_WithTenant_PrefixesWithTenantMarker()
    {
        var tenantId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        ICurrentTenant currentTenant = Substitute.For<ICurrentTenant>();
        currentTenant.IsAvailable.Returns(true);
        currentTenant.Id.Returns(tenantId);

        TenantAwareRoleLookupNormalizer normalizer = new(currentTenant);

        normalizer.NormalizeName("Manager").ShouldBe("T_11111111222233334444555555555555_MANAGER");
    }

    [Fact]
    public void NormalizeName_DifferentTenants_ProduceDifferentKeys()
    {
        ICurrentTenant tenantA = Substitute.For<ICurrentTenant>();
        tenantA.IsAvailable.Returns(true);
        tenantA.Id.Returns(Guid.NewGuid());

        ICurrentTenant tenantB = Substitute.For<ICurrentTenant>();
        tenantB.IsAvailable.Returns(true);
        tenantB.Id.Returns(Guid.NewGuid());

        TenantAwareRoleLookupNormalizer normalizerA = new(tenantA);
        TenantAwareRoleLookupNormalizer normalizerB = new(tenantB);

        normalizerA.NormalizeName("Manager").ShouldNotBe(normalizerB.NormalizeName("Manager"));
    }

    [Fact]
    public void NormalizeName_NullInput_ReturnsNull()
    {
        ICurrentTenant currentTenant = Substitute.For<ICurrentTenant>();
        currentTenant.IsAvailable.Returns(false);

        TenantAwareRoleLookupNormalizer normalizer = new(currentTenant);

        normalizer.NormalizeName(null).ShouldBeNull();
    }

    [Fact]
    public void NormalizeEmail_ReturnsUpperInvariant()
    {
        ICurrentTenant currentTenant = Substitute.For<ICurrentTenant>();
        currentTenant.IsAvailable.Returns(true);
        currentTenant.Id.Returns(Guid.NewGuid());

        TenantAwareRoleLookupNormalizer normalizer = new(currentTenant);

        normalizer.NormalizeEmail("Alice@Example.com").ShouldBe("ALICE@EXAMPLE.COM");
    }
}
