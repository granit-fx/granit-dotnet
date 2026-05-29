using System.Text.Json;
using Granit.Domain;
using Granit.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace Granit.Tests.Domain.ValueObjects;

public sealed class ImageDimensionsTests
{
    // -------------------------------------------------------------------------
    // Construction + validation
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(1200, 630)]
    [InlineData(0, 0)]
    [InlineData(1080, 1080)]
    public void Ctor_NonNegative_Succeeds(int width, int height)
    {
        var dimensions = new ImageDimensions(width, height);

        dimensions.Width.ShouldBe(width);
        dimensions.Height.ShouldBe(height);
    }

    [Theory]
    [InlineData(-1, 10)]
    [InlineData(10, -1)]
    public void Ctor_NegativeDimension_Throws(int width, int height)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new ImageDimensions(width, height));
    }

    // -------------------------------------------------------------------------
    // Derived geometry
    // -------------------------------------------------------------------------

    [Fact]
    public void AspectRatio_DividesWidthByHeight()
    {
        new ImageDimensions(1600, 800).AspectRatio.ShouldBe(2d);
    }

    [Fact]
    public void AspectRatio_ZeroHeight_ReturnsZeroNotInfinity()
    {
        new ImageDimensions(100, 0).AspectRatio.ShouldBe(0d);
    }

    [Theory]
    [InlineData(1200, 630, true, false, false)]
    [InlineData(630, 1200, false, true, false)]
    [InlineData(500, 500, false, false, true)]
    public void OrientationFlags_ReflectShape(
        int width, int height, bool landscape, bool portrait, bool square)
    {
        var dimensions = new ImageDimensions(width, height);

        dimensions.IsLandscape.ShouldBe(landscape);
        dimensions.IsPortrait.ShouldBe(portrait);
        dimensions.IsSquare.ShouldBe(square);
    }

    [Fact]
    public void TotalPixels_DoesNotOverflowInt()
    {
        // 50000 * 50000 = 2.5e9, beyond int.MaxValue (~2.147e9).
        new ImageDimensions(50_000, 50_000).TotalPixels.ShouldBe(2_500_000_000L);
    }

    [Fact]
    public void ToString_IsHumanReadable()
    {
        new ImageDimensions(1200, 630).ToString().ShouldBe("1200x630px");
    }

    // -------------------------------------------------------------------------
    // Value semantics
    // -------------------------------------------------------------------------

    [Fact]
    public void Equality_IsStructural()
    {
        var a = new ImageDimensions(1200, 630);
        var b = new ImageDimensions(1200, 630);
        var c = new ImageDimensions(1200, 631);

        a.ShouldBe(b);
        (a == b).ShouldBeTrue();
        a.ShouldNotBe(c);
    }

    [Fact]
    public void IsValueObject()
    {
        new ImageDimensions(1, 1).ShouldBeAssignableTo<ValueObject>();
    }

    // -------------------------------------------------------------------------
    // Serialization — only width/height; computed members are ignored
    // -------------------------------------------------------------------------

    [Fact]
    public void Json_SerializesWidthAndHeightOnly()
    {
        string json = JsonSerializer.Serialize(new ImageDimensions(1200, 630));

        json.ShouldContain("\"Width\":1200");
        json.ShouldContain("\"Height\":630");
        json.ShouldNotContain("AspectRatio");
        json.ShouldNotContain("TotalPixels");
        json.ShouldNotContain("IsLandscape");
    }

    [Fact]
    public void Json_RoundTrips()
    {
        var original = new ImageDimensions(1200, 630);

        ImageDimensions? restored = JsonSerializer.Deserialize<ImageDimensions>(
            JsonSerializer.Serialize(original));

        restored.ShouldBe(original);
    }
}
