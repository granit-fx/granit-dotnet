using Granit.BlobStorage.Validators;
using Shouldly;
using Xunit;

namespace Granit.BlobStorage.Tests.Validators;

public sealed class MagicByteDetectorAdditionalTests
{
    [Fact]
    public void Detect_Gif87aSignature_ReturnsGif()
    {
        byte[] buffer = [0x47, 0x49, 0x46, 0x38, 0x37, 0x61, 0x00, 0x00];

        string? result = MagicByteDetector.Detect(buffer);

        result.ShouldBe("image/gif");
    }

    [Fact]
    public void Detect_Gif89aSignature_ReturnsGif()
    {
        byte[] buffer = [0x47, 0x49, 0x46, 0x38, 0x39, 0x61, 0x00, 0x00];

        string? result = MagicByteDetector.Detect(buffer);

        result.ShouldBe("image/gif");
    }

    [Fact]
    public void Detect_TiffLittleEndian_ReturnsTiff()
    {
        byte[] buffer = [0x49, 0x49, 0x2A, 0x00, 0x08, 0x00];

        string? result = MagicByteDetector.Detect(buffer);

        result.ShouldBe("image/tiff");
    }

    [Fact]
    public void Detect_TiffBigEndian_ReturnsTiff()
    {
        byte[] buffer = [0x4D, 0x4D, 0x00, 0x2A, 0x00, 0x08];

        string? result = MagicByteDetector.Detect(buffer);

        result.ShouldBe("image/tiff");
    }

    [Fact]
    public void Detect_EmptyBuffer_ReturnsNull()
    {
        byte[] buffer = [];

        string? result = MagicByteDetector.Detect(buffer);

        result.ShouldBeNull();
    }

    [Fact]
    public void Detect_TooShortForPdf_ReturnsNull()
    {
        byte[] buffer = [0x25, 0x50, 0x44]; // %PD - truncated

        string? result = MagicByteDetector.Detect(buffer);

        result.ShouldBeNull();
    }

    [Fact]
    public void RequiredByteCount_Is261() => MagicByteDetector.RequiredByteCount.ShouldBe(261);
}
