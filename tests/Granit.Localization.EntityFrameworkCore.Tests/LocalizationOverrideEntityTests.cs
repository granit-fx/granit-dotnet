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
}
