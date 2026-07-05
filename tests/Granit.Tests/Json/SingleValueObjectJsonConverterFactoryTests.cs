using System.Text.Json;
using Granit.Domain;
using Granit.Domain.ValueObjects;
using Granit.Json;
using Shouldly;
using Xunit;

namespace Granit.Tests.Json;

public sealed class SingleValueObjectJsonConverterFactoryTests
{
    private static JsonSerializerOptions CreateOptions()
    {
        JsonSerializerOptions options = new();
        options.Converters.Add(new SingleValueObjectJsonConverterFactory());
        return options;
    }

    // -------------------------------------------------------------------------
    // CanConvert
    // -------------------------------------------------------------------------

    [Fact]
    public void CanConvert_SingleValueObjectSubclass_ReturnsTrue()
    {
        SingleValueObjectJsonConverterFactory factory = new();

        factory.CanConvert(typeof(ContentType)).ShouldBeTrue();
        factory.CanConvert(typeof(FileName)).ShouldBeTrue();
        factory.CanConvert(typeof(HttpsUrl)).ShouldBeTrue();
        factory.CanConvert(typeof(EntityTypeName)).ShouldBeTrue();
    }

    [Fact]
    public void CanConvert_NonSingleValueObject_ReturnsFalse()
    {
        SingleValueObjectJsonConverterFactory factory = new();

        factory.CanConvert(typeof(string)).ShouldBeFalse();
        factory.CanConvert(typeof(int)).ShouldBeFalse();
        factory.CanConvert(typeof(object)).ShouldBeFalse();
    }

    [Fact]
    public void CanConvert_ValueObjectBase_ReturnsFalse()
    {
        SingleValueObjectJsonConverterFactory factory = new();

        factory.CanConvert(typeof(ValueObject)).ShouldBeFalse();
    }

    // -------------------------------------------------------------------------
    // Serialization — writes primitive value directly
    // -------------------------------------------------------------------------

    [Fact]
    public void Serialize_ContentType_WritesRawString()
    {
        JsonSerializerOptions options = CreateOptions();
        var contentType = ContentType.Create("application/json");

        string json = JsonSerializer.Serialize(contentType, options);

        json.ShouldBe("\"application/json\"");
    }

    [Fact]
    public void Serialize_FileName_WritesRawString()
    {
        JsonSerializerOptions options = CreateOptions();
        var fileName = FileName.Create("report.pdf");

        string json = JsonSerializer.Serialize(fileName, options);

        json.ShouldBe("\"report.pdf\"");
    }

    [Fact]
    public void Serialize_HttpsUrl_WritesRawString()
    {
        JsonSerializerOptions options = CreateOptions();
        var url = HttpsUrl.Create("https://example.com");

        string json = JsonSerializer.Serialize(url, options);

        json.ShouldBe("\"https://example.com\"");
    }

    // -------------------------------------------------------------------------
    // Deserialization — reads primitive value and creates instance
    // -------------------------------------------------------------------------

    [Fact]
    public void Deserialize_ContentType_ReadsFromRawString()
    {
        JsonSerializerOptions options = CreateOptions();
        const string json = "\"text/html\"";

        ContentType? result = JsonSerializer.Deserialize<ContentType>(json, options);

        result.ShouldNotBeNull();
        result!.Value.ShouldBe("text/html");
    }

    [Fact]
    public void Deserialize_FileName_ReadsFromRawString()
    {
        JsonSerializerOptions options = CreateOptions();
        const string json = "\"image.png\"";

        FileName? result = JsonSerializer.Deserialize<FileName>(json, options);

        result.ShouldNotBeNull();
        result!.Value.ShouldBe("image.png");
    }

    [Fact]
    public void Deserialize_Null_ReturnsNull()
    {
        JsonSerializerOptions options = CreateOptions();
        const string json = "null";

        ContentType? result = JsonSerializer.Deserialize<ContentType>(json, options);

        result.ShouldBeNull();
    }

    // -------------------------------------------------------------------------
    // Round-trip
    // -------------------------------------------------------------------------

    [Fact]
    public void RoundTrip_ContentType_PreservesValue()
    {
        JsonSerializerOptions options = CreateOptions();
        var original = ContentType.Create("application/pdf");

        string json = JsonSerializer.Serialize(original, options);
        ContentType? deserialized = JsonSerializer.Deserialize<ContentType>(json, options);

        deserialized.ShouldNotBeNull();
        deserialized!.Value.ShouldBe(original.Value);
    }

    // -------------------------------------------------------------------------
    // In object graph
    // -------------------------------------------------------------------------

    [Fact]
    public void Serialize_InObjectGraph_WritesFlat()
    {
        JsonSerializerOptions options = CreateOptions();
        TestContainer container = new()
        {
            Name = "test",
            Type = ContentType.Create("text/plain"),
        };

        string json = JsonSerializer.Serialize(container, options);

        // The ContentType should be serialized as a flat string, not as {"value":"text/plain"}
        json.ShouldContain("\"text/plain\"");
        json.ShouldNotContain("\"Value\"");
    }

    // -------------------------------------------------------------------------
    // CreateConverter — non-SingleValueObject throws
    // -------------------------------------------------------------------------

    [Fact]
    public void CreateConverter_NonSingleValueObject_Throws()
    {
        SingleValueObjectJsonConverterFactory factory = new();

        Should.Throw<InvalidOperationException>(
            () => factory.CreateConverter(typeof(string), new JsonSerializerOptions()));
    }

    // -------------------------------------------------------------------------
    // Test fixtures
    // -------------------------------------------------------------------------

    private sealed class TestContainer
    {
        public string Name { get; init; } = string.Empty;
        public ContentType? Type { get; init; }
    }
}
