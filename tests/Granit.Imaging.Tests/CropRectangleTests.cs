using Shouldly;
using Xunit;

namespace Granit.Imaging.Tests;

public sealed class CropRectangleTests
{
    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        CropRectangle a = new(10, 20, 300, 200);
        CropRectangle b = new(10, 20, 300, 200);

        a.ShouldBe(b);
        (a == b).ShouldBeTrue();
    }

    [Fact]
    public void Equality_DifferentX_AreNotEqual()
    {
        CropRectangle a = new(10, 20, 300, 200);
        CropRectangle b = new(15, 20, 300, 200);

        a.ShouldNotBe(b);
        (a != b).ShouldBeTrue();
    }

    [Fact]
    public void Equality_DifferentY_AreNotEqual()
    {
        CropRectangle a = new(10, 20, 300, 200);
        CropRectangle b = new(10, 25, 300, 200);

        a.ShouldNotBe(b);
    }

    [Fact]
    public void Equality_DifferentWidth_AreNotEqual()
    {
        CropRectangle a = new(10, 20, 300, 200);
        CropRectangle b = new(10, 20, 400, 200);

        a.ShouldNotBe(b);
    }

    [Fact]
    public void Equality_DifferentHeight_AreNotEqual()
    {
        CropRectangle a = new(10, 20, 300, 200);
        CropRectangle b = new(10, 20, 300, 250);

        a.ShouldNotBe(b);
    }

    [Fact]
    public void Default_HasZeroValues()
    {
        CropRectangle rect = default;

        rect.X.ShouldBe(0);
        rect.Y.ShouldBe(0);
        rect.Width.ShouldBe(0);
        rect.Height.ShouldBe(0);
    }

    [Fact]
    public void ToString_ContainsAllValues()
    {
        CropRectangle rect = new(10, 20, 300, 200);

        string result = rect.ToString();

        result.ShouldContain("10");
        result.ShouldContain("20");
        result.ShouldContain("300");
        result.ShouldContain("200");
    }

    [Fact]
    public void GetHashCode_SameValues_SameHash()
    {
        CropRectangle a = new(10, 20, 300, 200);
        CropRectangle b = new(10, 20, 300, 200);

        a.GetHashCode().ShouldBe(b.GetHashCode());
    }
}
