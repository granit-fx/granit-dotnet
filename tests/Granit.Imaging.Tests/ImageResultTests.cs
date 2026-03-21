using Shouldly;
using Xunit;

namespace Granit.Imaging.Tests;

public sealed class ImageResultTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        byte[] content = [0x89, 0x50, 0x4E, 0x47];
        ReadOnlyMemory<byte> memory = content;

        ImageResult result = new(memory, ImageFormat.Png, 800, 600, "image.png");

        result.Content.ToArray().ShouldBe(content);
        result.Format.ShouldBe(ImageFormat.Png);
        result.Width.ShouldBe(800);
        result.Height.ShouldBe(600);
        result.FileName.ShouldBe("image.png");
    }

    [Fact]
    public void Constructor_FileNameDefaultsToNull()
    {
        ReadOnlyMemory<byte> content = new byte[] { 0x01 };

        ImageResult result = new(content, ImageFormat.Jpeg, 100, 100);

        result.FileName.ShouldBeNull();
    }

    [Fact]
    public void Constructor_EmptyContent_IsAllowed()
    {
        ReadOnlyMemory<byte> empty = ReadOnlyMemory<byte>.Empty;

        ImageResult result = new(empty, ImageFormat.WebP, 0, 0);

        result.Content.Length.ShouldBe(0);
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        byte[] content = [0x01, 0x02];
        ImageResult a = new(content, ImageFormat.Jpeg, 640, 480, "photo.jpg");
        ImageResult b = new(content, ImageFormat.Jpeg, 640, 480, "photo.jpg");

        a.ShouldBe(b);
    }

    [Fact]
    public void Equality_DifferentFormat_AreNotEqual()
    {
        byte[] content = [0x01, 0x02];
        ImageResult a = new(content, ImageFormat.Jpeg, 640, 480);
        ImageResult b = new(content, ImageFormat.Png, 640, 480);

        a.ShouldNotBe(b);
    }

    [Fact]
    public void Equality_DifferentDimensions_AreNotEqual()
    {
        byte[] content = [0x01, 0x02];
        ImageResult a = new(content, ImageFormat.Jpeg, 640, 480);
        ImageResult b = new(content, ImageFormat.Jpeg, 800, 600);

        a.ShouldNotBe(b);
    }

    [Fact]
    public void Equality_DifferentFileName_AreNotEqual()
    {
        byte[] content = [0x01, 0x02];
        ImageResult a = new(content, ImageFormat.Jpeg, 640, 480, "a.jpg");
        ImageResult b = new(content, ImageFormat.Jpeg, 640, 480, "b.jpg");

        a.ShouldNotBe(b);
    }

    [Fact]
    public void WithExpression_ChangesFormat()
    {
        byte[] content = [0x01, 0x02];
        ImageResult original = new(content, ImageFormat.Jpeg, 640, 480, "photo.jpg");

        ImageResult modified = original with { Format = ImageFormat.WebP };

        modified.Format.ShouldBe(ImageFormat.WebP);
        modified.Width.ShouldBe(640);
        modified.Height.ShouldBe(480);
        modified.FileName.ShouldBe("photo.jpg");
    }

    [Fact]
    public void AllImageFormats_CanBeUsedInResult()
    {
        ReadOnlyMemory<byte> content = new byte[] { 0x01 };

        ImageFormat[] formats =
        [
            ImageFormat.Jpeg,
            ImageFormat.Png,
            ImageFormat.WebP,
            ImageFormat.Avif,
            ImageFormat.Gif,
            ImageFormat.Bmp,
            ImageFormat.Tiff,
        ];

        foreach (ImageFormat format in formats)
        {
            ImageResult result = new(content, format, 1, 1);
            result.Format.ShouldBe(format);
        }
    }
}
