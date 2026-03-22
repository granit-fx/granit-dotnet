using Granit.Core.Domain;
using Granit.Persistence.ExtraProperties;
using Shouldly;
using Xunit;

namespace Granit.Persistence.Tests;

public sealed class ExtraPropertyMappingOptionsTests
{
    private sealed class TestEntity : IHasExtraProperties
    {
        public string? ExtraPropertiesJson { get; set; }
    }

    [Fact]
    public void MapProperty_AddsMapping()
    {
        ExtraPropertyMappingOptions<TestEntity> options = new();

        options.MapProperty<string>("Name", maxLength: 100, isRequired: true);

        options.Mappings.Count.ShouldBe(1);
        options.Mappings[0].Name.ShouldBe("Name");
        options.Mappings[0].ClrType.ShouldBe(typeof(string));
        options.Mappings[0].MaxLength.ShouldBe(100);
        options.Mappings[0].IsRequired.ShouldBeTrue();
    }

    [Fact]
    public void MapProperty_WithFilterableAndSortable()
    {
        ExtraPropertyMappingOptions<TestEntity> options = new();

        options.MapProperty<int>("Score", isFilterable: true, isSortable: true);

        options.Mappings[0].IsFilterable.ShouldBeTrue();
        options.Mappings[0].IsSortable.ShouldBeTrue();
        options.Mappings[0].MaxLength.ShouldBeNull();
        options.Mappings[0].IsRequired.ShouldBeFalse();
    }

    [Fact]
    public void MapProperty_ThrowsOnNullName()
    {
        ExtraPropertyMappingOptions<TestEntity> options = new();

        Should.Throw<ArgumentException>(() => options.MapProperty<string>(null!));
    }

    [Fact]
    public void MapProperty_MultipleProperties()
    {
        ExtraPropertyMappingOptions<TestEntity> options = new();

        options.MapProperty<string>("A");
        options.MapProperty<bool>("B");
        options.MapProperty<int>("C");

        options.Mappings.Count.ShouldBe(3);
    }
}
