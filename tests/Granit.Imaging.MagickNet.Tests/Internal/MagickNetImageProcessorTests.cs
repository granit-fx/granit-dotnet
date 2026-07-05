using System.Diagnostics.Metrics;
using Granit.Imaging.Diagnostics;
using Granit.Imaging.Exceptions;
using Granit.Imaging.MagickNet.Internal;
using Granit.Imaging.MagickNet.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Imaging.MagickNet.Tests.Internal;

public sealed class MagickNetImageProcessorTests
{
    private readonly MagickNetImageProcessor _processor = new(CreateTestMetrics(), new ImagingMagickNetOptions());

    private static ImagingMetrics CreateTestMetrics()
    {
        IMeterFactory factory = Substitute.For<IMeterFactory>();
        factory.Create(Arg.Any<MeterOptions>()).Returns(new Meter("test"));
        return new ImagingMetrics(factory);
    }

    private static Stream GetTestImageStream() =>
        typeof(MagickNetImageProcessorTests).Assembly
            .GetManifestResourceStream("Granit.Imaging.MagickNet.Tests.TestAssets.test-image.png")!;

    private static ReadOnlyMemory<byte> GetTestImageBytes()
    {
        using Stream stream = GetTestImageStream();
        using MemoryStream ms = new();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    [Fact]
    public void Load_FromStream_ReturnsPipelineWithCorrectSourceFormat()
    {
        // Arrange
        using Stream stream = GetTestImageStream();

        // Act
        IImagePipeline pipeline = _processor.Load(stream);

        // Assert
        pipeline.SourceFormat.ShouldBe(ImageFormat.Png);
        pipeline.SourceSize.Width.ShouldBe(100);
        pipeline.SourceSize.Height.ShouldBe(100);
    }

    [Fact]
    public void Load_FromReadOnlyMemory_ReturnsPipelineWithCorrectSourceSize()
    {
        // Arrange
        ReadOnlyMemory<byte> bytes = GetTestImageBytes();

        // Act
        IImagePipeline pipeline = _processor.Load(bytes);

        // Assert
        pipeline.SourceFormat.ShouldBe(ImageFormat.Png);
        pipeline.SourceSize.ShouldBe(new ImageSize(100, 100));
    }

    [Fact]
    public void Identify_FromReadOnlyMemory_ReturnsHeaderInfoWithoutDecoding()
    {
        // Arrange
        ReadOnlyMemory<byte> bytes = GetTestImageBytes();

        // Act
        ImageInfo info = _processor.Identify(bytes);

        // Assert
        info.Format.ShouldBe(ImageFormat.Png);
        info.Size.ShouldBe(new ImageSize(100, 100));
    }

    [Fact]
    public void Identify_UnknownFormat_ThrowsUnsupportedImageFormat()
    {
        // Arrange — random bytes with no recognized raster header.
        ReadOnlyMemory<byte> junk = new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07 };

        // Act / Assert
        Should.Throw<UnsupportedImageFormatException>(() => _processor.Identify(junk));
    }

    [Fact]
    public async Task Load_FromStream_PipelineIsDisposable()
    {
        // Arrange
        await using Stream stream = GetTestImageStream();

        // Act
        IImagePipeline pipeline = _processor.Load(stream);

        // Assert
        Func<Task> act = () => pipeline.DisposeAsync().AsTask();
        await Should.NotThrowAsync(act);
    }
}
