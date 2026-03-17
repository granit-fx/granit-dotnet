using System.IO.Compression;
using Granit.Http.ResponseCompression.Options;
using Shouldly;
using Xunit;

namespace Granit.Http.ResponseCompression.Tests;

public sealed class GranitResponseCompressionOptionsTests
{
    [Fact]
    public void SectionName_IsResponseCompression() =>
        GranitResponseCompressionOptions.SectionName.ShouldBe("ResponseCompression");

    [Fact]
    public void Defaults_EnableForHttps_IsTrue() =>
        new GranitResponseCompressionOptions().EnableForHttps.ShouldBeTrue();

    [Fact]
    public void Defaults_EnableBrotli_IsTrue() =>
        new GranitResponseCompressionOptions().EnableBrotli.ShouldBeTrue();

    [Fact]
    public void Defaults_EnableGzip_IsTrue() =>
        new GranitResponseCompressionOptions().EnableGzip.ShouldBeTrue();

    [Fact]
    public void Defaults_BrotliLevel_IsFastest() =>
        new GranitResponseCompressionOptions().BrotliLevel.ShouldBe(CompressionLevel.Fastest);

    [Fact]
    public void Defaults_GzipLevel_IsFastest() =>
        new GranitResponseCompressionOptions().GzipLevel.ShouldBe(CompressionLevel.Fastest);
}
