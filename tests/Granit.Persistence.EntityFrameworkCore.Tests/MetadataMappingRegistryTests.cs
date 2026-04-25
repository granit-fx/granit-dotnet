using Granit.Domain;
using Granit.Persistence.EntityFrameworkCore.Metadata;
using Shouldly;
using Xunit;

namespace Granit.Persistence.EntityFrameworkCore.Tests;

public sealed class MetadataMappingRegistryTests
{
    private sealed class TestEntity : IHasMetadata
    {
        public string? MetadataJson { get; set; }
    }

    private sealed class OtherEntity : IHasMetadata
    {
        public string? MetadataJson { get; set; }
    }

    [Fact]
    public void GetMappedPropertyNames_WhenNoRegistrations_ReturnsEmptySet()
    {
        MetadataMappingRegistry registry = new();

        HashSet<string> names = registry.GetMappedPropertyNames(typeof(TestEntity));

        names.ShouldBeEmpty();
    }

    [Fact]
    public void GetMappings_WhenNoRegistrations_ReturnsEmptyList()
    {
        MetadataMappingRegistry registry = new();

        IReadOnlyList<MetadataMapping> mappings = registry.GetMappings(typeof(TestEntity));

        mappings.ShouldBeEmpty();
    }

    [Fact]
    public void Register_AddsMappings()
    {
        MetadataMappingRegistry registry = new();
        List<MetadataMapping> mappings =
        [
            new("Alpha3Code", typeof(string), 3, false, true, false),
            new("Region", typeof(string), 100, false, true, true),
        ];

        registry.Register(typeof(TestEntity), mappings);

        registry.GetMappedPropertyNames(typeof(TestEntity)).ShouldBe(["Alpha3Code", "Region"]);
        registry.GetMappings(typeof(TestEntity)).Count.ShouldBe(2);
    }

    [Fact]
    public void Register_MultipleEntityTypes_AreIsolated()
    {
        MetadataMappingRegistry registry = new();
        registry.Register(typeof(TestEntity), [new("PropA", typeof(string), null, false, false, false)]);
        registry.Register(typeof(OtherEntity), [new("PropB", typeof(int), null, false, false, false)]);

        registry.GetMappedPropertyNames(typeof(TestEntity)).ShouldContain("PropA");
        registry.GetMappedPropertyNames(typeof(TestEntity)).ShouldNotContain("PropB");
        registry.GetMappedPropertyNames(typeof(OtherEntity)).ShouldContain("PropB");
    }

    [Fact]
    public void Register_MergesWithExisting()
    {
        MetadataMappingRegistry registry = new();
        registry.Register(typeof(TestEntity), [new("PropA", typeof(string), null, false, false, false)]);
        registry.Register(typeof(TestEntity), [new("PropB", typeof(int), null, false, false, false)]);

        HashSet<string> names = registry.GetMappedPropertyNames(typeof(TestEntity));
        names.Count.ShouldBe(2);
        names.ShouldContain("PropA");
        names.ShouldContain("PropB");
    }

    [Fact]
    public void Register_EmptyMappings_DoesNothing()
    {
        MetadataMappingRegistry registry = new();

        registry.Register(typeof(TestEntity), []);

        registry.GetMappedPropertyNames(typeof(TestEntity)).ShouldBeEmpty();
    }
}
