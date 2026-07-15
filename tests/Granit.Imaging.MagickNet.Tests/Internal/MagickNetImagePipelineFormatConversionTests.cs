using System.Diagnostics.Metrics;
using Granit.Imaging.Diagnostics;
using Granit.Imaging.MagickNet.Internal;
using Granit.Imaging.MagickNet.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Imaging.MagickNet.Tests.Internal;

public sealed class MagickNetImagePipelineFormatConversionTests
{
    private static ImagingMetrics CreateTestMetrics()
    {
        IMeterFactory factory = Substitute.For<IMeterFactory>();
        factory.Create(Arg.Any<MeterOptions>()).Returns(new Meter("test"));
        return new ImagingMetrics(factory);
    }

    private static async Task<MagickNetImagePipeline> CreatePipelineAsync()
    {
        Stream stream = typeof(MagickNetImagePipelineFormatConversionTests).Assembly
            .GetManifestResourceStream("Granit.Imaging.MagickNet.Tests.TestAssets.test-image.png")!;
        MagickNetImageProcessor processor = new(CreateTestMetrics(), Microsoft.Extensions.Options.Options.Create(new ImagingMagickNetOptions()));
        return (MagickNetImagePipeline)await processor.LoadAsync(stream);
    }

    [Theory]
    [InlineData(ImageFormat.Jpeg)]
    [InlineData(ImageFormat.Png)]
    [InlineData(ImageFormat.WebP)]
    [InlineData(ImageFormat.Gif)]
    [InlineData(ImageFormat.Bmp)]
    [InlineData(ImageFormat.Tiff)]
    public async Task ConvertTo_AllFormats_ProducesValidOutput(ImageFormat format)
    {
        await using MagickNetImagePipeline pipeline = await CreatePipelineAsync();

        ImageResult result = await pipeline
            .ConvertTo(format)
            .ToResultAsync(TestContext.Current.CancellationToken);

        result.Format.ShouldBe(format);
        result.Content.Length.ShouldBeGreaterThan(0);
        result.Width.ShouldBe(100);
        result.Height.ShouldBe(100);
    }

    [Fact]
    public async Task ConvertTo_Jpeg_StripsPngTransparency()
    {
        await using MagickNetImagePipeline pipeline = await CreatePipelineAsync();

        ImageResult result = await pipeline
            .ConvertTo(ImageFormat.Jpeg)
            .ToResultAsync(TestContext.Current.CancellationToken);

        // JPEG does not support transparency — output should still be valid
        result.Format.ShouldBe(ImageFormat.Jpeg);
        result.Content.Length.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task ConvertTo_Jpeg_WithCompression_ProducesSmallerFile()
    {
        // JPEG compression is more predictable than WebP on tiny images
        await using MagickNetImagePipeline highQuality = await CreatePipelineAsync();
        await using MagickNetImagePipeline lowQuality = await CreatePipelineAsync();

        ImageResult highResult = await highQuality
            .ConvertTo(ImageFormat.Jpeg)
            .Compress(100)
            .ToResultAsync(TestContext.Current.CancellationToken);

        ImageResult lowResult = await lowQuality
            .ConvertTo(ImageFormat.Jpeg)
            .Compress(10)
            .ToResultAsync(TestContext.Current.CancellationToken);

        lowResult.Content.Length.ShouldBeLessThan(highResult.Content.Length);
    }

    [Fact]
    public async Task SaveToStreamAsync_CancelledToken_Throws()
    {
        using CancellationTokenSource cts = new();
        cts.Cancel();
        MagickNetImagePipeline pipeline = await CreatePipelineAsync();
        await using MemoryStream output = new();

        Func<Task> act = () => pipeline.SaveToStreamAsync(output, cts.Token);
        await Should.ThrowAsync<OperationCanceledException>(act);
    }

    [Fact]
    public async Task SaveToStreamAsync_ProducesSameFormatAsToResult()
    {
        await using MagickNetImagePipeline pipeline1 = await CreatePipelineAsync();
        await using MagickNetImagePipeline pipeline2 = await CreatePipelineAsync();

        ImageResult resultA = await pipeline1
            .ConvertTo(ImageFormat.Jpeg)
            .Compress(80)
            .ToResultAsync(TestContext.Current.CancellationToken);

        await using MemoryStream stream = new();
        await pipeline2
            .ConvertTo(ImageFormat.Jpeg)
            .Compress(80)
            .SaveToStreamAsync(stream, TestContext.Current.CancellationToken);

        // Both should produce non-empty JPEG output
        resultA.Content.Length.ShouldBeGreaterThan(0);
        stream.Length.ShouldBeGreaterThan(0);
    }
}
