using System;
using System.IO;
using System.Threading.Tasks;
using Granit.Documents.AssetMetadata.AudioVideo.Internal;
using Granit.Documents.AssetMetadata.Extractors;
using Shouldly;
using TagLib;
using Xunit;

namespace Granit.Documents.AssetMetadata.AudioVideo.Tests;

public sealed class AudioVideoMetadataExtractorTests
{
    [Theory]
    [InlineData("audio/mpeg", true)]
    [InlineData("audio/flac", true)]
    [InlineData("audio/ogg", true)]
    [InlineData("audio/wav", true)]
    [InlineData("audio/x-wav", true)]
    [InlineData("audio/mp4", true)]
    [InlineData("video/mp4", true)]
    [InlineData("video/webm", true)]
    [InlineData("video/quicktime", true)]
    [InlineData("VIDEO/MP4", true)]
    [InlineData("image/jpeg", false)]
    [InlineData("image/png", false)]
    [InlineData("application/pdf", false)]
    [InlineData("application/vnd.openxmlformats-officedocument.wordprocessingml.document", false)]
    [InlineData("text/plain", false)]
    [InlineData("application/octet-stream", false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    public void CanHandle_matches_only_audio_and_video_mimes(string mime, bool expected) =>
        new AudioVideoMetadataExtractor().CanHandle(mime).ShouldBe(expected);

    [Fact]
    public async Task Extract_audio_wav_returns_typed_columns_and_audio_prefix_raw()
    {
        byte[] wav = AudioVideoFixtures.BuildWavWithTags(
            sampleRate: 8000,
            durationSeconds: 1,
            title: "Audio Title",
            artist: "Audio Artist",
            album: "Audio Album",
            genre: "Jazz",
            track: 5,
            year: 2026);

        await using MemoryStream stream = new(wav);
        AssetMetadataResult result = await new AudioVideoMetadataExtractor()
            .ExtractAsync(stream, "audio/wav", TestContext.Current.CancellationToken);

        result.ExtractorName.ShouldBe("audiovideo");
        result.Title.ShouldBe("Audio Title");
        result.Artist.ShouldBe("Audio Artist");
        result.Album.ShouldBe("Audio Album");
        result.Genre.ShouldBe("Jazz");
        result.TrackNumber.ShouldBe(5);
        result.TakenAt.ShouldNotBeNull();
        result.TakenAt!.Value.Year.ShouldBe(2026);
        result.DurationMs.ShouldNotBeNull();
        result.DurationMs!.Value.ShouldBeInRange(900, 1100);
        result.Codec.ShouldNotBeNullOrWhiteSpace();
        result.Bitrate.ShouldNotBeNull();
        result.Bitrate!.Value.ShouldBeGreaterThan(0);
        result.Width.ShouldBeNull();
        result.Height.ShouldBeNull();

        result.RawMetadata.ShouldContainKey("audio:Title");
        result.RawMetadata.ShouldContainKey("audio:Artist");
        result.RawMetadata.ShouldContainKey("audio:Album");
        result.RawMetadata.ShouldContainKey("audio:DurationMs");
        result.RawMetadata.ShouldContainKey("audio:Codec");
        result.RawMetadata.ShouldNotContainKey("video:Title");
    }

    [Fact]
    public async Task Extract_video_mp4_returns_typed_columns_and_video_prefix_raw()
    {
        byte[] mp4 = AudioVideoFixtures.MinimalMp4WithMetadata();

        await using MemoryStream stream = new(mp4);
        AssetMetadataResult result = await new AudioVideoMetadataExtractor()
            .ExtractAsync(stream, "video/mp4", TestContext.Current.CancellationToken);

        result.ExtractorName.ShouldBe("audiovideo");
        result.Title.ShouldBe("Granit AV F17.8");
        result.Artist.ShouldBe("JF Meyers");
        result.Album.ShouldBe("AssetMetadata");
        result.Genre.ShouldBe("Test");
        result.TakenAt.ShouldNotBeNull();
        result.TakenAt!.Value.Year.ShouldBe(2026);
        result.DurationMs.ShouldNotBeNull();
        result.DurationMs!.Value.ShouldBeInRange(1900, 2100);
        result.Width.ShouldBe(320);
        result.Height.ShouldBe(240);

        result.RawMetadata.ShouldContainKey("video:Title");
        result.RawMetadata.ShouldContainKey("video:Artist");
        result.RawMetadata.ShouldContainKey("video:VideoWidth");
        result.RawMetadata.ShouldContainKey("video:VideoHeight");
        result.RawMetadata["video:VideoWidth"].ShouldBe("320");
        result.RawMetadata["video:VideoHeight"].ShouldBe("240");
        result.RawMetadata.ShouldNotContainKey("audio:Title");
    }

    [Fact]
    public async Task Extract_works_with_non_seekable_stream()
    {
        byte[] wav = AudioVideoFixtures.BuildWavWithTags(
            sampleRate: 8000, durationSeconds: 1, title: "Stream Test");

        await using NonSeekableStream stream = new(wav);
        AssetMetadataResult result = await new AudioVideoMetadataExtractor()
            .ExtractAsync(stream, "audio/wav", TestContext.Current.CancellationToken);

        result.Title.ShouldBe("Stream Test");
        result.DurationMs.ShouldNotBeNull();
    }

    [Fact]
    public async Task Extract_surfaces_taglib_failure_as_runtime_error()
    {
        // Random bytes with a recognised extension still fail TagLib's container parse.
        byte[] garbage = new byte[64];
        new Random(42).NextBytes(garbage);
        await using MemoryStream stream = new(garbage);

        var extractor = new AudioVideoMetadataExtractor();

        // TagLib throws an UnsupportedFormatException / CorruptFileException; the
        // pipeline wraps the result into AssetMetadataExtractionException. The
        // extractor itself just lets the underlying exception bubble up.
        await Should.ThrowAsync<Exception>(async () =>
            await extractor.ExtractAsync(stream, "audio/mpeg", TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Implements_IAssetMetadataExtractor()
    {
        AudioVideoMetadataExtractor extractor = new();
        extractor.ShouldBeAssignableTo<IAssetMetadataExtractor>();
        extractor.Name.ShouldBe("audiovideo");
    }

    private sealed class NonSeekableStream(byte[] data) : Stream
    {
        private readonly MemoryStream _inner = new(data);

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }
        public override void Flush() => _inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
