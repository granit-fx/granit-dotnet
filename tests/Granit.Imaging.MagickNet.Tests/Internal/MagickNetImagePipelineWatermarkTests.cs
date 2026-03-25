using System.Diagnostics.Metrics;
using Granit.Imaging.Diagnostics;
using Granit.Imaging.MagickNet.Internal;
using ImageMagick;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Imaging.MagickNet.Tests.Internal;

public sealed class MagickNetImagePipelineWatermarkTests
{
    private static ImagingMetrics CreateTestMetrics()
    {
        IMeterFactory factory = Substitute.For<IMeterFactory>();
        factory.Create(Arg.Any<MeterOptions>()).Returns(new Meter("test"));
        return new ImagingMetrics(factory);
    }

    private static MagickNetImagePipeline CreatePipeline()
    {
        Stream stream = typeof(MagickNetImagePipelineWatermarkTests).Assembly
            .GetManifestResourceStream("Granit.Imaging.MagickNet.Tests.TestAssets.test-image.png")!;
        MagickNetImageProcessor processor = new(CreateTestMetrics());
        return (MagickNetImagePipeline)processor.Load(stream);
    }

    private static ReadOnlyMemory<byte> CreateWatermarkBytes()
    {
        // Create a small 10x10 red PNG as watermark
        using MagickImage img = new(MagickColors.Red, 10, 10);
        img.Format = MagickFormat.Png;
        return img.ToByteArray();
    }

    private static MemoryStream CreateWatermarkStream()
    {
        using MagickImage img = new(MagickColors.Red, 10, 10);
        img.Format = MagickFormat.Png;
        return new MemoryStream(img.ToByteArray());
    }

    // ── All WatermarkPosition values ────────────────────────────────────────

    [Theory]
    [InlineData(WatermarkPosition.Center)]
    [InlineData(WatermarkPosition.TopLeft)]
    [InlineData(WatermarkPosition.TopRight)]
    [InlineData(WatermarkPosition.BottomLeft)]
    [InlineData(WatermarkPosition.BottomRight)]
    public async Task Watermark_AllPositions_ProduceValidOutput(WatermarkPosition position)
    {
        await using MagickNetImagePipeline pipeline = CreatePipeline();
        ReadOnlyMemory<byte> watermark = CreateWatermarkBytes();

        ImageResult result = await pipeline
            .Watermark(watermark, position, 0.5f)
            .ToResultAsync(TestContext.Current.CancellationToken);

        result.Content.Length.ShouldBeGreaterThan(0);
        result.Width.ShouldBe(100);
        result.Height.ShouldBe(100);
    }

    // ── Opacity variants ────────────────────────────────────────────────────

    [Theory]
    [InlineData(0.0f)]
    [InlineData(0.25f)]
    [InlineData(0.5f)]
    [InlineData(1.0f)]
    public async Task Watermark_DifferentOpacities_ProduceValidOutput(float opacity)
    {
        await using MagickNetImagePipeline pipeline = CreatePipeline();
        ReadOnlyMemory<byte> watermark = CreateWatermarkBytes();

        ImageResult result = await pipeline
            .Watermark(watermark, WatermarkPosition.Center, opacity)
            .ToResultAsync(TestContext.Current.CancellationToken);

        result.Content.Length.ShouldBeGreaterThan(0);
    }

    // ── Stream overload ─────────────────────────────────────────────────────

    [Fact]
    public async Task Watermark_FromStream_ProducesValidOutput()
    {
        await using MagickNetImagePipeline pipeline = CreatePipeline();
        using MemoryStream watermarkStream = CreateWatermarkStream();

        ImageResult result = await pipeline
            .Watermark(watermarkStream, WatermarkPosition.BottomRight, 0.5f)
            .ToResultAsync(TestContext.Current.CancellationToken);

        result.Content.Length.ShouldBeGreaterThan(0);
    }

    // ── Watermark preserves dimensions ──────────────────────────────────────

    [Fact]
    public async Task Watermark_DoesNotChangeDimensions()
    {
        await using MagickNetImagePipeline pipeline = CreatePipeline();
        ReadOnlyMemory<byte> watermark = CreateWatermarkBytes();

        ImageResult result = await pipeline
            .Watermark(watermark)
            .ToResultAsync(TestContext.Current.CancellationToken);

        result.Width.ShouldBe(pipeline.SourceSize.Width);
        result.Height.ShouldBe(pipeline.SourceSize.Height);
    }

    // ── Chaining with other operations ──────────────────────────────────────

    [Fact]
    public async Task Watermark_AfterResize_WorksCorrectly()
    {
        await using MagickNetImagePipeline pipeline = CreatePipeline();
        ReadOnlyMemory<byte> watermark = CreateWatermarkBytes();

        ImageResult result = await pipeline
            .Resize(50, 50)
            .Watermark(watermark, WatermarkPosition.Center, 0.3f)
            .ConvertTo(ImageFormat.Jpeg)
            .ToResultAsync(TestContext.Current.CancellationToken);

        result.Width.ShouldBe(50);
        result.Height.ShouldBe(50);
        result.Format.ShouldBe(ImageFormat.Jpeg);
        result.Content.Length.ShouldBeGreaterThan(0);
    }
}
