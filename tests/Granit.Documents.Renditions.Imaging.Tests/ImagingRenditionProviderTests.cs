using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Granit.Documents.Renditions;
using Granit.Documents.Renditions.Domain;
using Granit.Documents.Renditions.Imaging.Internal;
using Granit.Imaging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Documents.Renditions.Imaging.Tests;

public sealed class ImagingRenditionProviderTests
{
    private static IImageProcessor BuildProcessor(out IImagePipeline pipeline, int outWidth = 200, int outHeight = 200)
    {
        pipeline = Substitute.For<IImagePipeline>();
        pipeline.StripMetadata().Returns(pipeline);
        pipeline.ConvertTo(Arg.Any<ImageFormat>()).Returns(pipeline);
        pipeline.Resize(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<ResizeMode>()).Returns(pipeline);
        pipeline.Compress(Arg.Any<int>()).Returns(pipeline);
        pipeline.ToResultAsync(Arg.Any<CancellationToken>())
            .Returns(new ImageResult(new byte[] { 1, 2, 3, 4 }, ImageFormat.WebP, outWidth, outHeight));

        IImageProcessor processor = Substitute.For<IImageProcessor>();
        processor.Load(Arg.Any<Stream>()).Returns(pipeline);
        return processor;
    }

    [Theory]
    [InlineData("image/png", true)]
    [InlineData("image/jpeg", true)]
    [InlineData("IMAGE/WEBP", true)]
    [InlineData("application/pdf", false)]
    [InlineData("", false)]
    public void CanHandle_recognises_image_mimes(string contentType, bool expected) =>
        new ImagingRenditionProvider(Substitute.For<IImageProcessor>())
            .CanHandle(contentType).ShouldBe(expected);

    [Fact]
    public async Task GenerateAsync_strips_metadata_converts_resizes_and_compresses()
    {
        IImageProcessor processor = BuildProcessor(out IImagePipeline pipeline);
        var provider = new ImagingRenditionProvider(processor);
        using MemoryStream src = new(new byte[] { 0xFF });
        var target = new RenditionTarget(RenditionType.Thumbnail, "image/webp", new RenditionDimensions(200, 200), Quality: 75);

        RenditionResult result = await provider.GenerateAsync(src, "image/png", target, CancellationToken.None);

        result.ContentType.ShouldBe("image/webp");
        result.Content.Length.ShouldBe(4);
        result.Width.ShouldBe(200);
        result.Height.ShouldBe(200);

        Received.InOrder(() =>
        {
            pipeline.StripMetadata();
            pipeline.ConvertTo(ImageFormat.WebP);
            pipeline.Resize(200, 200, ResizeMode.Max);
            pipeline.Compress(75);
            pipeline.ToResultAsync(Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task GenerateAsync_skips_resize_when_dimensions_null()
    {
        IImageProcessor processor = BuildProcessor(out IImagePipeline pipeline);
        var provider = new ImagingRenditionProvider(processor);
        using MemoryStream src = new(new byte[] { 0xFF });
        var target = new RenditionTarget(RenditionType.Web, "image/webp");

        await provider.GenerateAsync(src, "image/jpeg", target, CancellationToken.None);

        pipeline.DidNotReceiveWithAnyArgs().Resize(0, 0);
        pipeline.DidNotReceiveWithAnyArgs().Compress(0);
    }

    [Theory]
    [InlineData("image/jpeg", ImageFormat.Jpeg)]
    [InlineData("image/png", ImageFormat.Png)]
    [InlineData("image/webp", ImageFormat.WebP)]
    [InlineData("image/avif", ImageFormat.Avif)]
    [InlineData("image/gif", ImageFormat.Gif)]
    [InlineData("image/bmp", ImageFormat.Bmp)]
    [InlineData("image/tiff", ImageFormat.Tiff)]
    public async Task GenerateAsync_maps_mime_to_image_format(string mime, ImageFormat format)
    {
        IImageProcessor processor = BuildProcessor(out IImagePipeline pipeline);
        var provider = new ImagingRenditionProvider(processor);
        using MemoryStream src = new(new byte[] { 0xFF });
        var target = new RenditionTarget(RenditionType.Web, mime);

        await provider.GenerateAsync(src, "image/png", target, CancellationToken.None);

        pipeline.Received(1).ConvertTo(format);
    }

    [Fact]
    public async Task GenerateAsync_throws_for_unsupported_mime()
    {
        IImageProcessor processor = BuildProcessor(out _);
        var provider = new ImagingRenditionProvider(processor);
        using MemoryStream src = new(new byte[] { 0xFF });
        var target = new RenditionTarget(RenditionType.Thumbnail, "video/mp4");

        await Should.ThrowAsync<NotSupportedException>(() =>
            provider.GenerateAsync(src, "image/png", target, CancellationToken.None));
    }
}
