using Granit.Domain;
using Shouldly;
using Xunit;

namespace Granit.Tests.Domain;

public sealed class MetadataExtensionsTests
{
    private sealed class TestEntity : IHasMetadata
    {
        public string? MetadataJson { get; set; }
    }

    [Fact]
    public void GetMetadata_WhenJsonIsNull_ReturnsEmptyDictionary()
    {
        TestEntity entity = new();

        IReadOnlyDictionary<string, string> result = entity.GetMetadata();

        result.ShouldBeEmpty();
    }

    [Fact]
    public void GetMetadata_WhenJsonHasValues_ReturnsDictionary()
    {
        TestEntity entity = new() { MetadataJson = """{"Key1":"Value1","Key2":"Value2"}""" };

        IReadOnlyDictionary<string, string> result = entity.GetMetadata();

        result.Count.ShouldBe(2);
        result["Key1"].ShouldBe("Value1");
        result["Key2"].ShouldBe("Value2");
    }

    [Fact]
    public void SetMetadataValue_AddsNewProperty()
    {
        TestEntity entity = new();

        entity.SetMetadataValue("Department", "Engineering");

        entity.MetadataJson.ShouldNotBeNull();
        entity.GetMetadataValue("Department").ShouldBe("Engineering");
    }

    [Fact]
    public void SetMetadataValue_UpdatesExistingProperty()
    {
        TestEntity entity = new() { MetadataJson = """{"Department":"Sales"}""" };

        entity.SetMetadataValue("Department", "Engineering");

        entity.GetMetadataValue("Department").ShouldBe("Engineering");
    }

    [Fact]
    public void SetMetadataValue_WithNull_RemovesProperty()
    {
        TestEntity entity = new() { MetadataJson = """{"Department":"Sales","Level":"Senior"}""" };

        entity.SetMetadataValue("Department", null);

        entity.HasMetadataValue("Department").ShouldBeFalse();
        entity.HasMetadataValue("Level").ShouldBeTrue();
    }

    [Fact]
    public void SetMetadataValue_RemovingLastProperty_SetsJsonToNull()
    {
        TestEntity entity = new() { MetadataJson = """{"Department":"Sales"}""" };

        entity.SetMetadataValue("Department", null);

        entity.MetadataJson.ShouldBeNull();
    }

    [Fact]
    public void HasMetadataValue_WhenExists_ReturnsTrue()
    {
        TestEntity entity = new() { MetadataJson = """{"Key":"Value"}""" };

        entity.HasMetadataValue("Key").ShouldBeTrue();
    }

    [Fact]
    public void HasMetadataValue_WhenNotExists_ReturnsFalse()
    {
        TestEntity entity = new();

        entity.HasMetadataValue("MissingKey").ShouldBeFalse();
    }

    [Fact]
    public void GetMetadataValue_String_ReturnsValue()
    {
        TestEntity entity = new() { MetadataJson = """{"Name":"Alice"}""" };

        entity.GetMetadataValue("Name").ShouldBe("Alice");
    }

    [Fact]
    public void GetMetadataValue_String_WhenMissing_ReturnsNull()
    {
        TestEntity entity = new();

        entity.GetMetadataValue("Name").ShouldBeNull();
    }

    [Fact]
    public void GetMetadataValue_Generic_ParsesInt()
    {
        TestEntity entity = new() { MetadataJson = """{"Count":"42"}""" };

        entity.GetMetadataValue<int>("Count").ShouldBe(42);
    }

    [Fact]
    public void GetMetadataValue_Generic_WhenMissing_ReturnsDefault()
    {
        TestEntity entity = new();

        entity.GetMetadataValue<int>("Count").ShouldBe(0);
    }

    [Fact]
    public void GetMetadataValue_Generic_WhenUnparseable_ReturnsDefault()
    {
        TestEntity entity = new() { MetadataJson = """{"Count":"not-a-number"}""" };

        entity.GetMetadataValue<int>("Count").ShouldBe(0);
    }

    [Fact]
    public void SetMetadataValue_ThrowsOnNullName()
    {
        TestEntity entity = new();

        Should.Throw<ArgumentException>(() => entity.SetMetadataValue(null!, "value"));
    }

    [Fact]
    public void SetMetadataValue_ThrowsOnWhitespaceName()
    {
        TestEntity entity = new();

        Should.Throw<ArgumentException>(() => entity.SetMetadataValue("  ", "value"));
    }
}
