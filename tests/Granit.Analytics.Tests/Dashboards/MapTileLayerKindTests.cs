using System.Text.Json;
using System.Text.Json.Serialization;
using Granit.Analytics.Dashboards.Widgets;
using Shouldly;
using Xunit;

namespace Granit.Analytics.Tests.Dashboards;

/// <summary>
/// Pins the wire format of <see cref="MapTileLayerKind"/> per ADR-039 §6.1
/// (PascalCase enum strings via the framework's standard
/// <c>JsonStringEnumConverter()</c>). The frontend mirror in
/// <c>granit-front/packages/@granit/react-map/src/types.ts</c> consumes the
/// exact strings asserted here — any drift is a breaking wire change.
/// </summary>
public sealed class MapTileLayerKindTests
{
    private static readonly JsonSerializerOptions PascalCaseEnumOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };

    [Theory]
    [InlineData(MapTileLayerKind.Plan, "\"Plan\"")]
    [InlineData(MapTileLayerKind.Satellite, "\"Satellite\"")]
    [InlineData(MapTileLayerKind.Hybrid, "\"Hybrid\"")]
    [InlineData(MapTileLayerKind.Topo, "\"Topo\"")]
    [InlineData(MapTileLayerKind.Custom, "\"Custom\"")]
    public void Serializes_AsPascalCaseString(MapTileLayerKind kind, string expected)
    {
        string json = JsonSerializer.Serialize(kind, PascalCaseEnumOptions);
        json.ShouldBe(expected);
    }

    [Theory]
    [InlineData("\"Plan\"", MapTileLayerKind.Plan)]
    [InlineData("\"Satellite\"", MapTileLayerKind.Satellite)]
    [InlineData("\"Hybrid\"", MapTileLayerKind.Hybrid)]
    [InlineData("\"Topo\"", MapTileLayerKind.Topo)]
    [InlineData("\"Custom\"", MapTileLayerKind.Custom)]
    public void Deserializes_FromPascalCaseString(string json, MapTileLayerKind expected)
    {
        MapTileLayerKind result = JsonSerializer.Deserialize<MapTileLayerKind>(json, PascalCaseEnumOptions);
        result.ShouldBe(expected);
    }

    [Fact]
    public void Nullable_RoundTripsAsNull()
    {
        // Wire shape on MapWidgetSnapshot is nullable — ensure the null
        // round-trip is byte-identical so the frontend can rely on absence
        // = "no preference".
        MapTileLayerKind? input = null;

        string json = JsonSerializer.Serialize(input, PascalCaseEnumOptions);
        json.ShouldBe("null");

        MapTileLayerKind? back = JsonSerializer.Deserialize<MapTileLayerKind?>(json, PascalCaseEnumOptions);
        back.ShouldBeNull();
    }
}
