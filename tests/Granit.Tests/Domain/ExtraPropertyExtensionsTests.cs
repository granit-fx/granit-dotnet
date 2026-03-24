using Granit.Domain;
using Shouldly;
using Xunit;

namespace Granit.Tests.Domain;

public sealed class ExtraPropertyExtensionsTests
{
    private sealed class TestEntity : IHasExtraProperties
    {
        public string? ExtraPropertiesJson { get; set; }
    }

    [Fact]
    public void GetExtraProperties_WhenJsonIsNull_ReturnsEmptyDictionary()
    {
        TestEntity entity = new();

        IReadOnlyDictionary<string, string> result = entity.GetExtraProperties();

        result.ShouldBeEmpty();
    }

    [Fact]
    public void GetExtraProperties_WhenJsonHasValues_ReturnsDictionary()
    {
        TestEntity entity = new() { ExtraPropertiesJson = """{"Key1":"Value1","Key2":"Value2"}""" };

        IReadOnlyDictionary<string, string> result = entity.GetExtraProperties();

        result.Count.ShouldBe(2);
        result["Key1"].ShouldBe("Value1");
        result["Key2"].ShouldBe("Value2");
    }

    [Fact]
    public void SetExtraProperty_AddsNewProperty()
    {
        TestEntity entity = new();

        entity.SetExtraProperty("Department", "Engineering");

        entity.ExtraPropertiesJson.ShouldNotBeNull();
        entity.GetExtraProperty("Department").ShouldBe("Engineering");
    }

    [Fact]
    public void SetExtraProperty_UpdatesExistingProperty()
    {
        TestEntity entity = new() { ExtraPropertiesJson = """{"Department":"Sales"}""" };

        entity.SetExtraProperty("Department", "Engineering");

        entity.GetExtraProperty("Department").ShouldBe("Engineering");
    }

    [Fact]
    public void SetExtraProperty_WithNull_RemovesProperty()
    {
        TestEntity entity = new() { ExtraPropertiesJson = """{"Department":"Sales","Level":"Senior"}""" };

        entity.SetExtraProperty("Department", null);

        entity.HasExtraProperty("Department").ShouldBeFalse();
        entity.HasExtraProperty("Level").ShouldBeTrue();
    }

    [Fact]
    public void SetExtraProperty_RemovingLastProperty_SetsJsonToNull()
    {
        TestEntity entity = new() { ExtraPropertiesJson = """{"Department":"Sales"}""" };

        entity.SetExtraProperty("Department", null);

        entity.ExtraPropertiesJson.ShouldBeNull();
    }

    [Fact]
    public void HasExtraProperty_WhenExists_ReturnsTrue()
    {
        TestEntity entity = new() { ExtraPropertiesJson = """{"Key":"Value"}""" };

        entity.HasExtraProperty("Key").ShouldBeTrue();
    }

    [Fact]
    public void HasExtraProperty_WhenNotExists_ReturnsFalse()
    {
        TestEntity entity = new();

        entity.HasExtraProperty("MissingKey").ShouldBeFalse();
    }

    [Fact]
    public void GetExtraProperty_String_ReturnsValue()
    {
        TestEntity entity = new() { ExtraPropertiesJson = """{"Name":"Alice"}""" };

        entity.GetExtraProperty("Name").ShouldBe("Alice");
    }

    [Fact]
    public void GetExtraProperty_String_WhenMissing_ReturnsNull()
    {
        TestEntity entity = new();

        entity.GetExtraProperty("Name").ShouldBeNull();
    }

    [Fact]
    public void GetExtraProperty_Generic_ParsesInt()
    {
        TestEntity entity = new() { ExtraPropertiesJson = """{"Count":"42"}""" };

        entity.GetExtraProperty<int>("Count").ShouldBe(42);
    }

    [Fact]
    public void GetExtraProperty_Generic_WhenMissing_ReturnsDefault()
    {
        TestEntity entity = new();

        entity.GetExtraProperty<int>("Count").ShouldBe(0);
    }

    [Fact]
    public void GetExtraProperty_Generic_WhenUnparseable_ReturnsDefault()
    {
        TestEntity entity = new() { ExtraPropertiesJson = """{"Count":"not-a-number"}""" };

        entity.GetExtraProperty<int>("Count").ShouldBe(0);
    }

    [Fact]
    public void SetExtraProperty_ThrowsOnNullName()
    {
        TestEntity entity = new();

        Should.Throw<ArgumentException>(() => entity.SetExtraProperty(null!, "value"));
    }

    [Fact]
    public void SetExtraProperty_ThrowsOnWhitespaceName()
    {
        TestEntity entity = new();

        Should.Throw<ArgumentException>(() => entity.SetExtraProperty("  ", "value"));
    }
}
