using Granit.Localization.Domain;
using Shouldly;
using Xunit;

namespace Granit.Localization.EntityFrameworkCore.Tests;

public sealed class LocalizationOverrideEntityTests
{
    [Fact]
    public void DefaultProperties_AreEmpty()
    {
        LocalizationOverride entity = new();

        entity.ResourceName.ShouldBe(string.Empty);
        entity.CultureName.ShouldBe(string.Empty);
        entity.Key.ShouldBe(string.Empty);
        entity.Value.ShouldBe(string.Empty);
        entity.TenantId.ShouldBeNull();
    }

    [Fact]
    public void Properties_CanBeSet()
    {
        var tenantId = Guid.NewGuid();

        LocalizationOverride entity = new()
        {
            ResourceName = "TestApp",
            CultureName = "fr",
            Key = "Hello",
            Value = "Bonjour",
            TenantId = tenantId,
        };

        entity.ResourceName.ShouldBe("TestApp");
        entity.CultureName.ShouldBe("fr");
        entity.Key.ShouldBe("Hello");
        entity.Value.ShouldBe("Bonjour");
        entity.TenantId.ShouldBe(tenantId);
    }

    [Fact]
    public void ImplementsIMultiTenant()
    {
        LocalizationOverride entity = new();

        entity.ShouldBeAssignableTo<Granit.Domain.IMultiTenant>();
    }

    [Fact]
    public void ImplementsIEmitEntityLifecycleEvents()
    {
        LocalizationOverride entity = new();

        entity.ShouldBeAssignableTo<Granit.Domain.IEmitEntityLifecycleEvents>();
    }

    [Fact]
    public void InheritsFromAuditedEntity()
    {
        LocalizationOverride entity = new();

        entity.ShouldBeAssignableTo<Granit.Domain.AuditedEntity>();
    }

    [Fact]
    public void AuditFields_CanBeSet()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        LocalizationOverride entity = new()
        {
            Id = Guid.NewGuid(),
            CreatedAt = now,
            CreatedBy = "admin",
            ModifiedAt = now,
            ModifiedBy = "admin",
        };

        entity.Id.ShouldNotBe(Guid.Empty);
        entity.CreatedAt.ShouldBe(now);
        entity.CreatedBy.ShouldBe("admin");
        entity.ModifiedAt.ShouldBe(now);
        entity.ModifiedBy.ShouldBe("admin");
    }
}
