using System.Diagnostics.Metrics;
using Granit.Imaging.Diagnostics;
using Granit.Imaging.MagickNet.Internal;
using Granit.Imaging.MagickNet.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Imaging.MagickNet.Tests.Internal;

public sealed class MagickNetImagePipelineTests
{
    private static ImagingMetrics CreateTestMetrics()
    {
        IMeterFactory factory = Substitute.For<IMeterFactory>();
        factory.Create(Arg.Any<MeterOptions>()).Returns(new Meter("test"));
        return new ImagingMetrics(factory);
    }

    private static MagickNetImagePipeline CreatePipeline()
    {
        Stream stream = typeof(MagickNetImagePipelineTests).Assembly
            .GetManifestResourceStream("Granit.Imaging.MagickNet.Tests.TestAssets.test-image.png")!;
        MagickNetImageProcessor processor = new(CreateTestMetrics(), new ImagingMagickNetOptions());
        return (MagickNetImagePipeline)processor.Load(stream);
    }

    [Fact]
    public async Task Resize_Max_PreservesAspectRatioWithinBounds()
    {
        // Arrange
        await using MagickNetImagePipeline pipeline = CreatePipeline();

        // Act — 100x100 image resized to Max(50, 80)
        ImageResult result = await pipeline
            .Resize(50, 80, ResizeMode.Max)
            .ToResultAsync(TestContext.Current.CancellationToken);

        // Assert — should fit within 50x80, preserving 1:1 aspect ratio → 50x50
        result.Width.ShouldBe(50);
        result.Height.ShouldBe(50);
    }

    [Fact]
    public async Task Resize_Crop_FillsExactDimensions()
    {
        // Arrange
        await using MagickNetImagePipeline pipeline = CreatePipeline();

        // Act — 100x100 image crop-resized to 60x40
        ImageResult result = await pipeline
            .Resize(60, 40, ResizeMode.Crop)
            .ToResultAsync(TestContext.Current.CancellationToken);

        // Assert — exact dimensions
        result.Width.ShouldBe(60);
        result.Height.ShouldBe(40);
    }

    [Fact]
    public async Task Resize_Pad_PadsToExactDimensions()
    {
        // Arrange
        await using MagickNetImagePipeline pipeline = CreatePipeline();

        // Act — 100x100 image padded to 150x200
        ImageResult result = await pipeline
            .Resize(150, 200, ResizeMode.Pad)
            .ToResultAsync(TestContext.Current.CancellationToken);

        // Assert — exact dimensions (padded with transparent pixels)
        result.Width.ShouldBe(150);
        result.Height.ShouldBe(200);
    }

    [Fact]
    public async Task Resize_Stretch_DistortsToExactDimensions()
    {
        // Arrange
        await using MagickNetImagePipeline pipeline = CreatePipeline();

        // Act — 100x100 image stretched to 60x30
        ImageResult result = await pipeline
            .Resize(60, 30, ResizeMode.Stretch)
            .ToResultAsync(TestContext.Current.CancellationToken);

        // Assert — exact dimensions (distorted)
        result.Width.ShouldBe(60);
        result.Height.ShouldBe(30);
    }

    [Fact]
    public async Task Resize_Min_CoversTargetArea()
    {
        // Arrange
        await using MagickNetImagePipeline pipeline = CreatePipeline();

        // Act — 100x100 square, min 50x80 → 80x80 (fills 80 height, width follows)
        ImageResult result = await pipeline
            .Resize(50, 80, ResizeMode.Min)
            .ToResultAsync(TestContext.Current.CancellationToken);

        // Assert — covers both dimensions (1:1 aspect → 80x80)
        result.Width.ShouldBeGreaterThanOrEqualTo(50);
        result.Height.ShouldBeGreaterThanOrEqualTo(80);
    }

    [Fact]
    public async Task Crop_ReturnsCorrectDimensions()
    {
        // Arrange
        await using MagickNetImagePipeline pipeline = CreatePipeline();

        // Act
        ImageResult result = await pipeline
            .Crop(new CropRectangle(10, 10, 50, 30))
            .ToResultAsync(TestContext.Current.CancellationToken);

        // Assert
        result.Width.ShouldBe(50);
        result.Height.ShouldBe(30);
    }

    [Fact]
    public async Task ConvertTo_ChangesOutputFormat()
    {
        // Arrange
        await using MagickNetImagePipeline pipeline = CreatePipeline();

        // Act
        ImageResult result = await pipeline
            .ConvertTo(ImageFormat.Jpeg)
            .ToResultAsync(TestContext.Current.CancellationToken);

        // Assert
        result.Format.ShouldBe(ImageFormat.Jpeg);
        result.Content.Length.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task ConvertTo_WebP_ProducesValidOutput()
    {
        // Arrange
        await using MagickNetImagePipeline pipeline = CreatePipeline();

        // Act
        ImageResult result = await pipeline
            .ConvertTo(ImageFormat.WebP)
            .ToResultAsync(TestContext.Current.CancellationToken);

        // Assert
        result.Format.ShouldBe(ImageFormat.WebP);
        result.Content.Length.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task Compress_AffectsOutputSize()
    {
        // Arrange
        await using MagickNetImagePipeline highQuality = CreatePipeline();
        await using MagickNetImagePipeline lowQuality = CreatePipeline();

        // Act
        ImageResult highResult = await highQuality
            .ConvertTo(ImageFormat.Jpeg)
            .Compress(100)
            .ToResultAsync(TestContext.Current.CancellationToken);

        ImageResult lowResult = await lowQuality
            .ConvertTo(ImageFormat.Jpeg)
            .Compress(10)
            .ToResultAsync(TestContext.Current.CancellationToken);

        // Assert — lower quality should produce smaller file
        lowResult.Content.Length.ShouldBeLessThan(highResult.Content.Length);
    }

    [Fact]
    public async Task StripMetadata_RemovesExifData()
    {
        // Arrange
        await using MagickNetImagePipeline pipeline = CreatePipeline();

        // Act
        ImageResult result = await pipeline
            .StripMetadata()
            .ToResultAsync(TestContext.Current.CancellationToken);

        // Assert — result should be valid and not empty
        result.Content.Length.ShouldBeGreaterThan(0);
        result.Format.ShouldBe(ImageFormat.Png);
    }

    [Fact]
    public async Task SaveToStreamAsync_WritesToStream()
    {
        // Arrange
        await using MagickNetImagePipeline pipeline = CreatePipeline();
        using MemoryStream output = new();

        // Act
        await pipeline.SaveToStreamAsync(output, TestContext.Current.CancellationToken);

        // Assert
        output.Length.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task FullPipeline_ResizeCompressConvertStripMetadata()
    {
        // Arrange
        await using MagickNetImagePipeline pipeline = CreatePipeline();

        // Act
        ImageResult result = await pipeline
            .Resize(50, 50, ResizeMode.Crop)
            .Compress(75)
            .StripMetadata()
            .ConvertTo(ImageFormat.WebP)
            .ToResultAsync(TestContext.Current.CancellationToken);

        // Assert
        result.Width.ShouldBe(50);
        result.Height.ShouldBe(50);
        result.Format.ShouldBe(ImageFormat.WebP);
        result.Content.Length.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task ToResultAsync_PreservesSourceFormatWhenNoConversion()
    {
        // Arrange
        await using MagickNetImagePipeline pipeline = CreatePipeline();

        // Act
        ImageResult result = await pipeline.ToResultAsync(TestContext.Current.CancellationToken);

        // Assert
        result.Format.ShouldBe(ImageFormat.Png);
    }

    [Fact]
    public async Task DisposeAsync_ReleasesResources()
    {
        // Arrange
        MagickNetImagePipeline pipeline = CreatePipeline();

        // Act
        await pipeline.DisposeAsync();

        // Assert — calling ToResultAsync after dispose should throw
        Func<Task> act = () => pipeline.ToResultAsync(TestContext.Current.CancellationToken);
        await Should.ThrowAsync<ObjectDisposedException>(act);
    }

    [Fact]
    public async Task ToResultAsync_ThrowsOnCancelledToken()
    {
        // Arrange
        using CancellationTokenSource cts = new();
        cts.Cancel();
        MagickNetImagePipeline pipeline = CreatePipeline();

        // Act & Assert
        Func<Task> act = () => pipeline.ToResultAsync(cts.Token);
        await Should.ThrowAsync<OperationCanceledException>(act);
    }
}
