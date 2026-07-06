using Shouldly;
using Xunit;

namespace Granit.Imaging.Tests;

public sealed class ImageSizeTests
{
    [Fact]
    public void Equality_SameDimensions_AreEqual()
    {
        ImageSize a = new(1024, 768);
        ImageSize b = new(1024, 768);

        a.ShouldBe(b);
        (a == b).ShouldBeTrue();
    }

    [Fact]
    public void Equality_DifferentWidth_AreNotEqual()
    {
        ImageSize a = new(1024, 768);
        ImageSize b = new(800, 768);

        a.ShouldNotBe(b);
        (a != b).ShouldBeTrue();
    }

    [Fact]
    public void Equality_DifferentHeight_AreNotEqual()
    {
        ImageSize a = new(1024, 768);
        ImageSize b = new(1024, 600);

        a.ShouldNotBe(b);
    }

    [Fact]
    public void Default_HasZeroDimensions()
    {
        ImageSize size = default;

        size.Width.ShouldBe(0);
        size.Height.ShouldBe(0);
    }

    [Fact]
    public void ToString_ContainsDimensions()
    {
        ImageSize size = new(1920, 1080);

        string result = size.ToString();

        result.ShouldContain("1920");
        result.ShouldContain("1080");
    }

    [Fact]
    public void GetHashCode_SameDimensions_SameHash()
    {
        ImageSize a = new(640, 480);
        ImageSize b = new(640, 480);

        a.GetHashCode().ShouldBe(b.GetHashCode());
    }
}
