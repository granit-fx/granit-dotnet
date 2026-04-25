using Granit.Domain;
using Granit.Persistence.EntityFrameworkCore.Metadata;
using Shouldly;
using Xunit;

namespace Granit.Persistence.EntityFrameworkCore.Tests;

public sealed class MetadataMappingOptionsTests
{
    private sealed class TestEntity : IHasMetadata
    {
        public string? MetadataJson { get; set; }
    }

    [Fact]
    public void MapProperty_AddsMapping()
    {
        MetadataMappingOptions<TestEntity> options = new();

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
        MetadataMappingOptions<TestEntity> options = new();

        options.MapProperty<int>("Score", isFilterable: true, isSortable: true);

        options.Mappings[0].IsFilterable.ShouldBeTrue();
        options.Mappings[0].IsSortable.ShouldBeTrue();
        options.Mappings[0].MaxLength.ShouldBeNull();
        options.Mappings[0].IsRequired.ShouldBeFalse();
    }

    [Fact]
    public void MapProperty_ThrowsOnNullName()
    {
        MetadataMappingOptions<TestEntity> options = new();

        Should.Throw<ArgumentException>(() => options.MapProperty<string>(null!));
    }

    [Fact]
    public void MapProperty_MultipleProperties()
    {
        MetadataMappingOptions<TestEntity> options = new();

        options.MapProperty<string>("A");
        options.MapProperty<bool>("B");
        options.MapProperty<int>("C");

        options.Mappings.Count.ShouldBe(3);
    }
}
