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
    private readonly MagickNetImageProcessor _processor = new(CreateTestMetrics(), Microsoft.Extensions.Options.Options.Create(new ImagingMagickNetOptions()));

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
    public async Task LoadAsync_FromStream_ReturnsPipelineWithCorrectSourceFormat()
    {
        // Arrange
        await using Stream stream = GetTestImageStream();

        // Act
        IImagePipeline pipeline = await _processor.LoadAsync(stream, TestContext.Current.CancellationToken);

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
    public async Task LoadAsync_FromStream_PipelineIsDisposable()
    {
        // Arrange
        await using Stream stream = GetTestImageStream();

        // Act
        IImagePipeline pipeline = await _processor.LoadAsync(stream, TestContext.Current.CancellationToken);

        // Assert
        Func<Task> act = () => pipeline.DisposeAsync().AsTask();
        await Should.NotThrowAsync(act);
    }

    [Fact]
    public async Task LoadAsync_NonSeekableStream_BuffersAndDecodes()
    {
        await using Stream inner = GetTestImageStream();
        await using NonSeekableStream stream = new(inner);

        await using IImagePipeline pipeline = await _processor.LoadAsync(stream, TestContext.Current.CancellationToken);

        pipeline.SourceFormat.ShouldBe(ImageFormat.Png);
        pipeline.SourceSize.Width.ShouldBe(100);
    }

    [Fact]
    public async Task LoadAsync_SeekableStream_RestoresPositionAfterHeaderSniff()
    {
        await using Stream stream = GetTestImageStream();

        await using IImagePipeline pipeline = await _processor.LoadAsync(stream, TestContext.Current.CancellationToken);

        // The whole stream was consumed by the decode, not left at the 12-byte header mark.
        stream.Position.ShouldBe(stream.Length);
        pipeline.SourceFormat.ShouldBe(ImageFormat.Png);
    }

    [Fact]
    public async Task LoadAsync_StreamYieldingOneBytePerRead_StillPassesHeaderValidation()
    {
        // Regression: a single Read may legally return fewer than the requested bytes even
        // mid-stream; the header sniff must use ReadAtLeast semantics, not one Read call.
        await using Stream inner = GetTestImageStream();
        await using OneBytePerReadStream stream = new(inner);

        await using IImagePipeline pipeline = await _processor.LoadAsync(stream, TestContext.Current.CancellationToken);

        pipeline.SourceFormat.ShouldBe(ImageFormat.Png);
    }

    [Fact]
    public async Task LoadAsync_OversizedInput_Throws()
    {
        MagickNetImageProcessor processor = new(
            CreateTestMetrics(),
            Microsoft.Extensions.Options.Options.Create(new ImagingMagickNetOptions { MaxInputBytes = 16 }));
        await using Stream stream = GetTestImageStream();

        await Should.ThrowAsync<InvalidOperationException>(
            () => processor.LoadAsync(stream, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task LoadAsync_UnknownFormat_ThrowsUnsupportedImageFormat()
    {
        await using MemoryStream stream = new("%PDF-1.7 not an image"u8.ToArray());

        await Should.ThrowAsync<UnsupportedImageFormatException>(
            () => _processor.LoadAsync(stream, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task LoadAsync_CancelledToken_Throws()
    {
        await using Stream inner = GetTestImageStream();
        await using NonSeekableStream stream = new(inner);
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(
            () => _processor.LoadAsync(stream, cts.Token));
    }

    /// <summary>Wraps a stream and reports it as non-seekable (network-stream shape).</summary>
    private sealed class NonSeekableStream(Stream inner) : Stream
    {
        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    /// <summary>Seekable stream that returns at most one byte per Read call.</summary>
    private sealed class OneBytePerReadStream(Stream inner) : Stream
    {
        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => inner.Length;
        public override long Position
        {
            get => inner.Position;
            set => inner.Position = value;
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            inner.Read(buffer, offset, Math.Min(count, 1));

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
