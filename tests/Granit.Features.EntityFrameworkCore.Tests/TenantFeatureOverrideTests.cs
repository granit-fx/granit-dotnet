using Granit.Domain;
using Granit.Features.EntityFrameworkCore.Entities;
using Shouldly;
using Xunit;

namespace Granit.Features.EntityFrameworkCore.Tests;

public sealed class TenantFeatureOverrideTests
{
    [Fact]
    public void Implements_IMultiTenant()
    {
        typeof(TenantFeatureOverride)
            .IsAssignableTo(typeof(IMultiTenant))
            .ShouldBeTrue();
    }

    [Fact]
    public void Inherits_AuditedEntity()
    {
        typeof(TenantFeatureOverride)
            .IsAssignableTo(typeof(AuditedEntity))
            .ShouldBeTrue();
    }

    [Fact]
    public void DefaultValues_AreEmpty()
    {
        TenantFeatureOverride entity = new();

        entity.FeatureName.ShouldBe(string.Empty);
        entity.Value.ShouldBe(string.Empty);
        entity.TenantId.ShouldBeNull();
    }

    [Fact]
    public void Properties_CanBeSetAndRead()
    {
        var tenantId = Guid.NewGuid();
        var id = Guid.NewGuid();

        TenantFeatureOverride entity = new()
        {
            Id = id,
            TenantId = tenantId,
            FeatureName = "Acme.MaxUsersCount",
            Value = "5000",
        };

        entity.Id.ShouldBe(id);
        entity.TenantId.ShouldBe(tenantId);
        entity.FeatureName.ShouldBe("Acme.MaxUsersCount");
        entity.Value.ShouldBe("5000");
    }

    [Fact]
    public void TenantId_CanBeNull_ForGlobalOverride()
    {
        TenantFeatureOverride entity = new()
        {
            TenantId = null,
            FeatureName = "Global.Feature",
            Value = "enabled",
        };

        entity.TenantId.ShouldBeNull();
    }

    [Fact]
    public void Class_IsSealed() =>
        typeof(TenantFeatureOverride).IsSealed.ShouldBeTrue();

    [Fact]
    public void Class_IsInternal() =>
        typeof(TenantFeatureOverride).IsNotPublic.ShouldBeTrue();
}
