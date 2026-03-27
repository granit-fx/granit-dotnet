using System.Diagnostics.Metrics;
using Granit.Imaging.Diagnostics;
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
    public async Task Load_FromStream_PipelineIsDisposable()
    {
        // Arrange
        using Stream stream = GetTestImageStream();

        // Act
        IImagePipeline pipeline = _processor.Load(stream);

        // Assert
        Func<Task> act = () => pipeline.DisposeAsync().AsTask();
        await Should.NotThrowAsync(act);
    }
}
