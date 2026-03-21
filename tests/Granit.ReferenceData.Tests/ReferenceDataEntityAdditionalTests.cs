using Granit.Core.Domain;
using Granit.ReferenceData.Domain;
using Shouldly;
using Xunit;

namespace Granit.ReferenceData.Tests;

public sealed class ReferenceDataEntityAdditionalTests
{
    private sealed class TestEntity : ReferenceDataEntity;

    [Fact]
    public void Implements_IEmitEntityLifecycleEvents()
    {
        TestEntity entity = new();

        entity.ShouldBeAssignableTo<IEmitEntityLifecycleEvents>();
    }

    [Fact]
    public void Label_EnglishCulture_ReturnsLabelEn()
    {
        TestEntity entity = new() { LabelEn = "Belgium" };

        RunWithCulture("en-US", () => entity.Label.ShouldBe("Belgium"));
    }

    [Fact]
    public void ValidFrom_CanBeSet()
    {
        DateTimeOffset validFrom = DateTimeOffset.UtcNow;
        TestEntity entity = new() { ValidFrom = validFrom };

        entity.ValidFrom.ShouldBe(validFrom);
    }

    [Fact]
    public void ValidTo_CanBeSet()
    {
        DateTimeOffset validTo = DateTimeOffset.UtcNow.AddYears(1);
        TestEntity entity = new() { ValidTo = validTo };

        entity.ValidTo.ShouldBe(validTo);
    }

    [Fact]
    public void SortOrder_CanBeSet()
    {
        TestEntity entity = new() { SortOrder = 99 };

        entity.SortOrder.ShouldBe(99);
    }

    [Fact]
    public void IsActive_CanBeSetToFalse()
    {
        TestEntity entity = new() { IsActive = false };

        entity.IsActive.ShouldBeFalse();
    }

    [Fact]
    public void Id_CanBeSet()
    {
        var id = Guid.NewGuid();
        TestEntity entity = new() { Id = id };

        entity.Id.ShouldBe(id);
    }

    private static void RunWithCulture(string cultureName, Action action)
    {
        System.Globalization.CultureInfo previous = System.Globalization.CultureInfo.CurrentUICulture;
        try
        {
            System.Globalization.CultureInfo.CurrentUICulture = new System.Globalization.CultureInfo(cultureName);
            action();
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentUICulture = previous;
        }
    }
}
