using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Granit.Documents.AssetMetadata;
using Granit.Documents.AssetMetadata.Diagnostics;
using Granit.Documents.AssetMetadata.Exceptions;
using Granit.Documents.AssetMetadata.Extractors;
using Granit.Documents.AssetMetadata.Options;
using Granit.Documents.AssetMetadata.Pipeline;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Documents.AssetMetadata.Tests;

public sealed class AssetMetadataPipelineTests
{
    private sealed class TestMeterFactory : IMeterFactory
    {
        public Meter Create(MeterOptions options) => new(options);
        public void Dispose() { }
    }

    private static AssetMetadataMetrics BuildMetrics() =>
        new(new TestMeterFactory());

    private static IAssetMetadataExtractor Extractor(string name, string mimeFamily, AssetMetadataResult? result = null, Exception? throws = null)
    {
        IAssetMetadataExtractor x = Substitute.For<IAssetMetadataExtractor>();
        x.Name.Returns(name);
        x.CanHandle(Arg.Any<string>())
            .Returns(ci => ((string)ci[0]).StartsWith(mimeFamily, StringComparison.OrdinalIgnoreCase));
        if (throws is not null)
        {
            x.ExtractAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns<Task<AssetMetadataResult>>(_ => throw throws);
        }
        else
        {
            x.ExtractAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(result ?? new AssetMetadataResult(name, new Dictionary<string, string?>()));
        }
        return x;
    }

    [Fact]
    public async Task ExtractAsync_runs_only_matching_extractors_in_registration_order()
    {
        IAssetMetadataExtractor exif = Extractor("exif", "image/",
            new AssetMetadataResult("exif", new Dictionary<string, string?> { ["a"] = "1" }));
        IAssetMetadataExtractor pdf = Extractor("pdf", "application/pdf",
            new AssetMetadataResult("pdf", new Dictionary<string, string?> { ["b"] = "2" }));

        var pipeline = new AssetMetadataPipeline(
            [exif, pdf], BuildMetrics(), NullLogger<AssetMetadataPipeline>.Instance);

        using MemoryStream src = new(new byte[] { 0xFF });
        IReadOnlyList<AssetMetadataResult> results = await pipeline.ExtractAsync(
            src, "image/jpeg", CancellationToken.None);

        results.Count.ShouldBe(1);
        results[0].ExtractorName.ShouldBe("exif");
        results[0].RawMetadata["a"].ShouldBe("1");
    }

    [Fact]
    public async Task ExtractAsync_seeks_stream_back_between_extractors()
    {
        IAssetMetadataExtractor first = Extractor("first", "image/",
            new AssetMetadataResult("first", new Dictionary<string, string?>()));
        IAssetMetadataExtractor second = Extractor("second", "image/",
            new AssetMetadataResult("second", new Dictionary<string, string?>()));

        var pipeline = new AssetMetadataPipeline(
            [first, second], BuildMetrics(), NullLogger<AssetMetadataPipeline>.Instance);

        using MemoryStream src = new(new byte[] { 1, 2, 3, 4 });
        src.Position = 4;
        await pipeline.ExtractAsync(src, "image/jpeg", CancellationToken.None);

        // After seek + first run, stream is repositioned to 0 before the second extractor.
        await second.Received(1).ExtractAsync(
            Arg.Is<Stream>(s => s.Position == 0),
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExtractAsync_wraps_extractor_failure_in_AssetMetadataExtractionException()
    {
        IAssetMetadataExtractor exif = Extractor("exif", "image/",
            throws: new InvalidOperationException("boom"));

        var pipeline = new AssetMetadataPipeline(
            [exif], BuildMetrics(), NullLogger<AssetMetadataPipeline>.Instance);

        using MemoryStream src = new(new byte[] { 0xFF });
        AssetMetadataExtractionException ex = await Should.ThrowAsync<AssetMetadataExtractionException>(() =>
            pipeline.ExtractAsync(src, "image/jpeg", CancellationToken.None));
        ex.ExtractorName.ShouldBe("exif");
        ex.SourceContentType.ShouldBe("image/jpeg");
        ex.InnerException?.Message.ShouldBe("boom");
    }

    private static IOptions<GranitAssetMetadataOptions> Opts(GranitAssetMetadataOptions? o = null) =>
        Microsoft.Extensions.Options.Options.Create(o ?? new GranitAssetMetadataOptions());

    [Fact]
    public async Task ExtractAsync_strips_PII_keys_and_typed_columns_when_option_on()
    {
        Dictionary<string, string?> raw = new()
        {
            ["exif:Author"] = "Alice",
            ["exif:Make"] = "Canon",
            ["xmp:CreditLine"] = "Acme",
        };
        var result = new AssetMetadataResult("exif", raw)
        {
            Author = "Alice",
            Artist = "Alice",
            LastModifiedBy = "Bob",
            CameraMake = "Canon",
        };
        IAssetMetadataExtractor exif = Extractor("exif", "image/", result);

        var pipeline = new AssetMetadataPipeline(
            [exif], BuildMetrics(), NullLogger<AssetMetadataPipeline>.Instance,
            Opts(new GranitAssetMetadataOptions { StripPersonalDataOnUpload = true }));

        using MemoryStream src = new(new byte[] { 0xFF });
        IReadOnlyList<AssetMetadataResult> results = await pipeline.ExtractAsync(
            src, "image/jpeg", CancellationToken.None);

        results.Count.ShouldBe(1);
        AssetMetadataResult r = results[0];
        r.Author.ShouldBeNull();
        r.Artist.ShouldBeNull();
        r.LastModifiedBy.ShouldBeNull();
        r.CameraMake.ShouldBe("Canon");
        r.RawMetadata.ShouldNotContainKey("exif:Author");
        r.RawMetadata.ShouldNotContainKey("xmp:CreditLine");
        r.RawMetadata["exif:Make"].ShouldBe("Canon");
    }

    [Fact]
    public async Task ExtractAsync_preserves_PII_when_option_off()
    {
        Dictionary<string, string?> raw = new()
        {
            ["exif:Author"] = "Alice",
            ["exif:Make"] = "Canon",
        };
        var result = new AssetMetadataResult("exif", raw) { Author = "Alice" };
        IAssetMetadataExtractor exif = Extractor("exif", "image/", result);

        var pipeline = new AssetMetadataPipeline(
            [exif], BuildMetrics(), NullLogger<AssetMetadataPipeline>.Instance,
            Opts(new GranitAssetMetadataOptions { StripPersonalDataOnUpload = false }));

        using MemoryStream src = new(new byte[] { 0xFF });
        IReadOnlyList<AssetMetadataResult> results = await pipeline.ExtractAsync(
            src, "image/jpeg", CancellationToken.None);

        results[0].Author.ShouldBe("Alice");
        results[0].RawMetadata["exif:Author"].ShouldBe("Alice");
    }

    [Fact]
    public async Task ExtractAsync_throws_AssetMetadataExtractionTimeoutException_on_timeout()
    {
        // Extractor that blocks until cancellation is requested.
        IAssetMetadataExtractor slow = Substitute.For<IAssetMetadataExtractor>();
        slow.Name.Returns("slow");
        slow.CanHandle(Arg.Any<string>()).Returns(true);
        slow.ExtractAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(async ci =>
            {
                var token = (CancellationToken)ci[2];
                await Task.Delay(TimeSpan.FromSeconds(5), token).ConfigureAwait(false);
                return new AssetMetadataResult("slow", new Dictionary<string, string?>());
            });

        var pipeline = new AssetMetadataPipeline(
            [slow], BuildMetrics(), NullLogger<AssetMetadataPipeline>.Instance,
            Opts(new GranitAssetMetadataOptions { ExtractionTimeout = TimeSpan.FromMilliseconds(50) }));

        using MemoryStream src = new(new byte[] { 0xFF });
        await Should.ThrowAsync<Exceptions.AssetMetadataExtractionTimeoutException>(() =>
            pipeline.ExtractAsync(src, "image/jpeg", CancellationToken.None));
    }

    [Fact]
    public async Task ExtractAsync_returns_empty_when_no_extractor_matches()
    {
        IAssetMetadataExtractor onlyForPdf = Extractor("pdf", "application/pdf");

        var pipeline = new AssetMetadataPipeline(
            [onlyForPdf], BuildMetrics(), NullLogger<AssetMetadataPipeline>.Instance);

        using MemoryStream src = new(new byte[] { 0xFF });
        IReadOnlyList<AssetMetadataResult> results = await pipeline.ExtractAsync(
            src, "audio/mpeg", CancellationToken.None);

        results.ShouldBeEmpty();
    }
}
